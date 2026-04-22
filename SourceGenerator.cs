using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SaveDataGenerator
{
    [Generator]
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
            foreach (var tree in context.Compilation.SyntaxTrees)
            {
                var semanticModel = context.Compilation.GetSemanticModel(tree);
                var types = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();

                foreach (var typeDecl in types)
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                    if (typeSymbol == null) continue;

                    if (!HasSaveDataAttribute(typeSymbol)) continue;
                    
                    Log($"Generating {typeSymbol.Name}...");

                    var code = Generate(typeSymbol);
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        context.AddSource($"{typeSymbol.Name}.SaveData.g.cs", SourceText.From(code, Encoding.UTF8));
                        
                        Log($"Generated {typeSymbol.Name}.SaveData.g.cs\nContent:\n{code}");
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
                var typeInfo = ResolveType(m.Type);

                if (typeInfo.Skip) continue;

                if (!string.IsNullOrEmpty(typeInfo.Namespace))
                    usings.Add(typeInfo.Namespace);

                var typeName = GetShortTypeName(m.Type, usings); //typeInfo.TypeName;
                dtoFields.Add($"public {typeName} {m.Name} {{ get; set; }}");

                // ToSaveData
                string readExpr = $"model.{m.Name}";

                if (typeInfo.IsReactive)
                    readExpr += ".Value";

                if (typeInfo.IsNestedSaveData)
                    readExpr += ".ToSaveData()";

                toSaveLines.Add($"{m.Name} = {readExpr}");

                // ApplySaveData
                string writeExpr;

                if (typeInfo.IsReactive)
                {
                    writeExpr = $"model.{m.Name}.Value = {GetApplyValue(typeInfo, m.Name)};";
                }
                else if (m.SetMethod != null)
                {
                    writeExpr = $"model.{m.Name} = {GetApplyValue(typeInfo, m.Name)};";
                }
                else
                {
                    // get-only НЕ reactive → пропускаем
                    continue;
                }

                applyLines.Add(writeExpr);
            }

            if (dtoFields.Count == 0) return string.Empty;

            var sb = new StringBuilder();

            // =========================
            // USINGS
            // =========================

            foreach (var u in usings.OrderBy(x => x))
                sb.AppendLine($"using {u};");

            sb.AppendLine();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#pragma warning disable");

            if (ns != null)
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
            }

            // =========================
            // DTO
            // =========================

            sb.AppendLine($"public struct {dtoName}");
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
            sb.AppendLine($"        return new {dtoName}");
            sb.AppendLine("        {");

            foreach (var l in toSaveLines)
                sb.AppendLine($"            {l},");

            sb.AppendLine("        };");
            sb.AppendLine("    }");

            // Apply
            sb.AppendLine($"    public static void ApplySaveData(this {type.Name} model, {dtoName} data)");
            sb.AppendLine("    {");

            foreach (var l in applyLines)
                sb.AppendLine($"        {l}");

            sb.AppendLine("    }");

            sb.AppendLine("}");

            if (ns != null)
                sb.AppendLine("}");

            sb.AppendLine("#pragma warning restore");

            return sb.ToString();
        }
        
        private static readonly Dictionary<ITypeSymbol, string> _namesCache = new();

        private static string GetShortTypeName(ITypeSymbol typeInfo, HashSet<string> usings)
        {
            if (_namesCache.TryGetValue(typeInfo, out var name))
            {
                return name;
            }
            
            string typeName = typeInfo.Name;
            foreach (var ns in usings)
            {
                if (typeName.Contains(ns))
                {
                    typeName = typeName.Replace(ns, string.Empty);
                }
            }
            _namesCache[typeInfo] = typeName;
            return typeName;
        }

        // =========================
        // TYPE RESOLUTION
        // =========================

        private static TypeInfo ResolveType(ITypeSymbol type)
        {
            var info = new TypeInfo();

            if (type is not INamedTypeSymbol named)
            {
                info.TypeName = type.ToDisplayString();
                return info;
            }

            var typeName = named.Name;
            var fullName = named.ToDisplayString();

            info.Namespace = named.ContainingNamespace?.ToDisplayString();
            
            //TODO: configs process
            //if (named.)

            // --- ReactiveProperty<T>
            if (typeName.StartsWith("ReactiveProperty") && named.TypeArguments.Length == 1)
            {
                info.IsReactive = true;
                var inner = named.TypeArguments[0];
                var innerInfo = ResolveType(inner);

                info.TypeName = innerInfo.TypeName;
                info.IsNestedSaveData = innerInfo.IsNestedSaveData;
                return info;
            }

            // --- кастомные reactive (Vector3ReactiveProperty)
            if (typeName.EndsWith("ReactiveProperty"))
            {
                var valueProp = named.GetMembers("Value").OfType<IPropertySymbol>().FirstOrDefault();
                if (valueProp != null)
                {
                    info.IsReactive = true;
                    
                    var innerInfo = ResolveType(valueProp.Type);

                    Log($"Resolved {innerInfo.TypeName} as Value of {typeName}");
                    
                    info.TypeName = innerInfo.TypeName;
                    info.IsNestedSaveData = innerInfo.IsNestedSaveData;
                    return info;
                }
                else
                {
                    Log($"Cant resolve Value Type of {typeName}");
                }
            }

            // --- вложенные SaveData
            if (HasSaveDataAttribute(named))
            {
                info.IsNestedSaveData = true;
                info.TypeName = $"{named.Name}SaveData";
                return info;
            }

            // --- обычный тип
            info.TypeName = fullName;
            return info;
        }

        private static string GetApplyValue(TypeInfo info, string name)
        {
            if (info.IsNestedSaveData)
                return $"data.{name}.ToModel()"; // 👈 можно потом сгенерить обратный маппер

            return $"data.{name}";
        }

        // =========================
        // HELPERS
        // =========================

        private static bool HasSaveDataAttribute(ISymbol symbol)
        {
            return symbol.GetAttributes().Any(a =>
                a.AttributeClass?.Name is "SaveData" or "SaveDataAttribute");
        }

        private class TypeInfo
        {
            public string TypeName;
            public string Namespace;
            public bool IsReactive;
            public bool IsNestedSaveData;
            public bool Skip;
        }
    }
}