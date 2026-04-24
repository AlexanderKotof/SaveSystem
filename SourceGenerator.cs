using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace SaveDataGenerator
{
    [Generator]
    [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
    public class SaveDataSourceGenerator : ISourceGenerator
    {
        private static string GetLogPath()
        {
            try
            {
                // Пытаемся писать в Assets/Generated (относительно корня проекта)
                return Path.GetFullPath(Path.Combine("Temp", "Generated", "SourceGen_Debug.log"));
            }
            catch
            {
                // Фолбэк в TEMP, если права или путь недоступны
                return Path.Combine(Path.GetTempPath(), "Unity_SourceGen_Debug.log");
            }
        }

        private static void Log(string message)
        {
            try
            {
                var logPath = GetLogPath();
                var dir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
                Console.WriteLine(message);
            }
            catch (Exception ex)
            {
                // Если даже файл не пишется, выводим в стандартный вывод (попадает в Editor.log)
                Console.WriteLine($"[SaveGen LogFail] {ex.Message} | {message}");
            }
        }
        
        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var created = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            
            foreach (var tree in context.Compilation.SyntaxTrees)
            {
                var semanticModel = context.Compilation.GetSemanticModel(tree);
                var types = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();

                foreach (var typeDecl in types)
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                    if (typeSymbol == null)
                        continue;

                    if (!HasSaveDataAttribute(typeSymbol))
                        continue;
                    
                    if (!created.Add(typeSymbol))
                        continue;

                    Log($"Generating {typeSymbol.Name}...");

                    try
                    {
                        var code = Generate(typeSymbol);

                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            context.AddSource($"{typeSymbol.Name}.SaveData.g.cs", SourceText.From(code, Encoding.UTF8));

                            Log($"Generated {typeSymbol.Name}.SaveData.g.cs\nContent:\n{code}");
                        }
                        else
                        {
                            Log($"Generated empty output for {typeSymbol.Name}");
                        }
                    }
                    catch (Exception e)
                    {
                        Log("Exception: " + e.Message);
                    }
                }
            }
        }

        // =========================
        // CORE GENERATION
        // =========================

        private static string Generate(INamedTypeSymbol type)
        {
            var dtoName = $"{type.Name}SaveData";
            var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
            
            var members = type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(HasSaveDataAttribute)
                .ToList();

            //Collecting props from parent types
            var iterate = type;
            while (iterate.BaseType != null)
            {
                iterate = iterate.BaseType;

                var ms = iterate.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(HasSaveDataAttribute)
                    .ToList();
                
                members.AddRange(ms);
            }

            if (members.Count == 0) return string.Empty;

            var usings = new HashSet<string>
            {
                "System"
            };

            var dtoFields = new List<string>();
            var toSaveLines = new List<string>();
            var applyLines = new List<string>();

            foreach (var m in members)
            {
                CollectTypeContent(m, usings, dtoFields, toSaveLines, applyLines);
            }

            if (dtoFields.Count == 0) 
                return string.Empty;

            return GenerateOutput(type, usings, ns, dtoName, dtoFields, toSaveLines, applyLines);
        }

        private static void CollectTypeContent(IPropertySymbol m, HashSet<string> usings, List<string> dtoFields, List<string> toSaveLines,
            List<string> applyLines)
        {
            var typeInfo = ResolveType(m.Type, usings);

            if (typeInfo.Skip) return;

            var typeName = GetShortTypeName(typeInfo, usings);
                
            var dtoPropName = typeInfo.IsConfig ? m.Name + "Id" : m.Name;
            dtoFields.Add($"public {typeName} {dtoPropName} {{ get; set; }}");

            // ToSaveData
            string readExpr = $"model.{m.Name}";

            if (typeInfo.IsReactive)
                readExpr += ".Value";

            if (typeInfo.IsNestedSaveData)
                readExpr += ".ToSaveData()";
                
            if (typeInfo.IsConfig)
                readExpr += ".Id";

            toSaveLines.Add($"{dtoPropName} = {readExpr}");

            // ApplySaveData
            string writeExpr = GetWriteExpression(m, typeInfo, dtoPropName);

            if (!string.IsNullOrEmpty(writeExpr))
            {
                applyLines.Add(writeExpr);
            }
        }

        private static string GetWriteExpression(IPropertySymbol m, TypeInfo typeInfo, string dtoPropName)
        {
            if (typeInfo.IsNestedSaveData)
            {
                //TODO: skip for now
                return $"//*** Nested Data Exist: model.{m.Name} = {GetApplyValue(typeInfo, dtoPropName)};";
            }

            if (typeInfo.IsReactive)
            {
                return $"model.{m.Name}.Value = {GetApplyValue(typeInfo, dtoPropName)};";
            }
            
            if (typeInfo.IsConfig)
            {
                //Reading only, left processing configs to higher layer
                Log($"Config found {m.Name}, skipping!");
                
                // return $"model.{m.Name} = _resolver.Resolve({GetApplyValue(typeInfo, m.Name)});";
                return string.Empty;
            }

            if (m.SetMethod is { DeclaredAccessibility: Accessibility.Public })
            {
                return $"model.{m.Name} = {GetApplyValue(typeInfo, dtoPropName)};";
            }

            // get-only НЕ reactive → пропускаем
            Log($"Trying to update get-only non reactive property {m.Name}!");
            return $"//*** Data Exist: model.{m.Name} = {GetApplyValue(typeInfo, dtoPropName)}; (Not writable)";
        }

        private static string GenerateOutput(INamedTypeSymbol type, HashSet<string> usings, string? ns, string dtoName, List<string> dtoFields,
            List<string> toSaveLines, List<string> applyLines)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine();
            
            // =========================
            // USINGS
            // =========================

            foreach (var u in usings.OrderBy(x => x))
                sb.AppendLine($"using {u};");

            sb.AppendLine();
            sb.AppendLine("#pragma warning disable");

            if (ns != null)
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
            }

            // =========================
            // DTO
            // =========================

            sb.AppendLine("[Serializable]");
            sb.AppendLine($"public struct {dtoName} : ISaveData");
            sb.AppendLine("{");

            foreach (var f in dtoFields)
                sb.AppendLine($"    {f}");

            sb.AppendLine("}");

            // =========================
            // EXTENSIONS
            // =========================

            sb.AppendLine($"public static class {type.Name}SaveExtensions");
            sb.AppendLine("{");

            // ToSaveData
            sb.AppendLine($"    public static {dtoName} ToSaveData(this {type.Name} model)");
            sb.AppendLine("    {");
            sb.AppendLine("         if (model == null) return default;");
            sb.AppendLine($"        return new {dtoName}");
            sb.AppendLine("        {");

            foreach (var l in toSaveLines)
                sb.AppendLine($"            {l},");

            sb.AppendLine("        };");
            sb.AppendLine("    }");

            // Apply
            sb.AppendLine($"    public static void ApplySaveData(this {type.Name} model, {dtoName} data)");
            sb.AppendLine("    {");
            sb.AppendLine("         if (model == null) return;");
            foreach (var l in applyLines)
                sb.AppendLine($"        {l}");

            sb.AppendLine("    }");

            sb.AppendLine("}");

            if (ns != null)
                sb.AppendLine("}");

            sb.AppendLine("#pragma warning restore");

            return sb.ToString();
        }

        private static readonly Dictionary<string, string> _namesCache = new();

        private static string GetShortTypeName(TypeInfo typeInfo, HashSet<string> usings)
        {
            var typeName = typeInfo.TypeName;
            
            if (_namesCache.TryGetValue(typeName, out var name))
            {
                return name;
            }
            
            foreach (var ns in usings)
            {
                if (Equals(ns, "System"))
                    continue;
                
                string replace = ns + ".";
                if (typeName.Contains(replace))
                {
                    typeName = typeName.Replace(replace, string.Empty);
                }
            }
            _namesCache[typeName] = typeName;
            return typeName;
        }

        // =========================
        // TYPE RESOLUTION
        // =========================

        private static TypeInfo ResolveType(ITypeSymbol type, HashSet<string> usings)
        {
            if (type == null || type.Kind == SymbolKind.ErrorType)
                return TypeInfo.SkipType();
            
            var info = new TypeInfo();
            usings.Add(type.ContainingNamespace?.ToDisplayString());

            if (type is not INamedTypeSymbol named)
            {
                info.TypeName = type.ToDisplayString();
                return info;
            }
            
            string defName = named.OriginalDefinition?.ToDisplayString() ?? string.Empty;
            string typeName = named.Name;
            
            // Пропускаем коллекции
            if (defName.Contains("ReactiveCollection") || typeName.Contains("ReactiveCollection") || 
                defName.Contains("ReactiveDictionary") || typeName.Contains("ReactiveDictionary"))
                return TypeInfo.SkipType();
            
            // --- вложенные SaveData 
            
            //TODO: check for apply posibility
            if (HasSaveDataAttribute(named))
            {
                info.IsNestedSaveData = true;
                info.TypeName = $"{named.Name}SaveData";
                return info;
            }
            
            // --- вложенные SaveData
            if (IsConfig(named))
            {
                info.IsConfig = true;
                info.TypeName = $"string";
                return info;
            }
            
            // UniRx ReactiveProperty<T>
            if ((defName.Contains("ReactiveProperty") || typeName.Contains("ReactiveProperty")) && named.TypeArguments.Length > 0)
            {
                var arg = named.TypeArguments[0];
                if (arg == null || arg.Kind == SymbolKind.ErrorType)
                    return TypeInfo.SkipType();
      
                var innerInfo = ResolveType(arg, usings);
                innerInfo.IsReactive = true;
                return innerInfo;
            }

            // Кастомные реактивные (Vector3ReactiveProperty и т.д.)
            if (typeName.EndsWith("ReactiveProperty"))
            {
                var baseType = named.BaseType;
                return ResolveType(baseType!, usings);
            }
            
            info.TypeName = named.ToDisplayString();
            info.Skip = string.IsNullOrEmpty(info.TypeName);
            return info;
        }

        private static bool IsConfig(INamedTypeSymbol named)
        {
            return named.Name.Contains("Config") || named.ContainingNamespace.ToDisplayString().Contains("Configs");
        }

        private static string GetApplyValue(TypeInfo info, string name)
        {
            if (info.IsNestedSaveData)
                return $"data.{name}.ToModel()"; // нужно потом сгенерить обратный маппер

            return $"data.{name}";
        }

        // =========================
        // HELPERS
        // =========================

        private static bool HasSaveDataAttribute(ISymbol symbol) =>
            symbol.GetAttributes().Any(a =>
                a.AttributeClass?.Name is "SaveData" or "SaveDataAttribute");

        private class TypeInfo
        {
            public string TypeName;
            public bool IsReactive;
            public bool IsNestedSaveData;
            public bool Skip;
            public bool IsConfig;
            
            public static TypeInfo SkipType() => new() { Skip = true };
        }
    }
}