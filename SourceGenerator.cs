using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
            Log("=== SAVE_DATA_GENERATOR: START ===");

            foreach (var tree in context.Compilation.SyntaxTrees)
            {
                var sourceText = tree.GetText().ToString();
                if (!sourceText.Contains("[SaveData")) continue;

                var semanticModel = context.Compilation.GetSemanticModel(tree);
                var typeDeclarations = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();

                foreach (var typeDecl in typeDeclarations)
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                    if (typeSymbol is null) continue;

                    bool hasAttr = typeSymbol.GetAttributes().Any(a =>
                        a.AttributeClass?.Name is "SaveData" or "SaveDataAttribute");
                    if (!hasAttr) continue;

                    Log($"[FOUND] Processing: {typeSymbol.Name} ({typeSymbol.ContainingNamespace})");

                    try
                    {
                        string generatedCode = GenerateSaveData(typeSymbol, context);
                        
                        if (string.IsNullOrWhiteSpace(generatedCode))
                        {
                            Log($"[SKIP] No valid members for {typeSymbol.Name}. Generation skipped.");
                            continue;
                        }

                        Log($"[GEN] === BEGIN {typeSymbol.Name}.SaveData.g.cs ===");
                        Log(generatedCode);
                        Log($"[GEN] === END {typeSymbol.Name}.SaveData.g.cs ===");

                        context.AddSource($"{typeSymbol.Name}.SaveData.g.cs", SourceText.From(generatedCode, Encoding.UTF8));
                        Log($"[SUCCESS] Added to compilation context.");
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Generation failed for {typeSymbol.Name}: {ex}");
                        context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor("SDG_E001", "SaveData Gen Error", ex.ToString(), "SaveData", DiagnosticSeverity.Error, true),
                            typeDecl.GetLocation()));
                    }
                }
            }
            Log("=== SAVE_DATA_GENERATOR: END ===");
        }

        private static string GenerateSaveData(INamedTypeSymbol typeSymbol, GeneratorExecutionContext context)
        {
            var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace ? null : typeSymbol.ContainingNamespace.ToDisplayString();
            var dtoName = $"{typeSymbol.Name}SaveData";

            // Фильтруем только public, не static, не const, не readonly
            var members = typeSymbol.GetMembers()
                .Where(m => (m is IFieldSymbol || m is IPropertySymbol) && m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic)
                .Select(m => (Symbol: m, Type: GetSafeType(m)))
                .Where(x => x.Type != null && x.Type.Kind != SymbolKind.ErrorType) // 🔑 Защита от крача Unity
                .Where(x => x.Type is not INamedTypeSymbol named || named.TypeKind != TypeKind.Error)
                .Where(x => HasSaveDataAttribute(x.Symbol))
                .ToList();

            if (members.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#pragma warning disable CS0169, CS0649, CS8618");
            
            if (ns != null) sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            // 1. DTO Struct
            sb.AppendLine($"    public struct {dtoName}");
            sb.AppendLine("    {");
            foreach (var m in members)
            {
                var serializedType = GetSerializedType(m.Type!, out _);
                if (string.IsNullOrEmpty(serializedType))
                {
                    Log($"[WARN] Skipped {m.Symbol.Name} ({m.Type}) - unsupported type");
                    continue;
                }
                sb.AppendLine($"        public {serializedType} {m.Symbol.Name} {{ get; set; }}");
            }
            sb.AppendLine("    }");

            // 2. Extension Methods
            sb.AppendLine($"    public static class {typeSymbol.Name}SaveExtensions");
            sb.AppendLine("    {");
            
            // ToSaveData
            sb.AppendLine($"        public static {dtoName} ToSaveData(this {typeSymbol.Name} model)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return new {dtoName}");
            sb.AppendLine("            {");
            foreach (var m in members)
            {
                var serializedType = GetSerializedType(m.Type!, out bool isReactive);
                if (string.IsNullOrEmpty(serializedType)) continue;
                sb.AppendLine($"                {m.Symbol.Name} = model.{m.Symbol.Name}{(isReactive ? ".Value" : "")},");
            }
            sb.AppendLine("            };");
            sb.AppendLine("        }");

            // ApplySaveData
            sb.AppendLine($"        public static void ApplySaveData(this {typeSymbol.Name} model, {dtoName} data)");
            sb.AppendLine("        {");
            foreach (var m in members)
            {
                var serializedType = GetSerializedType(m.Type!, out bool isReactive);
                if (string.IsNullOrEmpty(serializedType)) continue;
                sb.AppendLine($"            model.{m.Symbol.Name}{(isReactive ? ".Value" : "")} = data.{m.Symbol.Name};");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");

            sb.AppendLine("}");
            sb.AppendLine("#pragma warning restore");

            return sb.ToString();
        }

        private static ITypeSymbol? GetSafeType(ISymbol member) => member switch
        {
            IPropertySymbol p => p.Type,
            IFieldSymbol f => f.Type,
            _ => null
        };
        
        private static bool HasSaveDataAttribute(ISymbol symbol)
        {
            return symbol.GetAttributes().Any(a =>
                a.AttributeClass?.Name is "SaveData" or "SaveDataAttribute");
        }
        
        // private static bool IsSettable(ISymbol symbol) => symbol switch
        // {
        //     IPropertySymbol ps => ps.SetMethod != null,
        //     IFieldSymbol fs => !fs.IsReadOnly && !fs.IsConst,
        //     _ => false
        // };

        private static string GetSerializedType(ITypeSymbol type, out bool isReactive)
        {
            isReactive = false;
            if (type == null || type.Kind == SymbolKind.ErrorType) return string.Empty;

            if (type is INamedTypeSymbol named)
            {
                string defName = named.OriginalDefinition?.ToDisplayString() ?? string.Empty;
                string typeName = named.Name;

                // Пропускаем коллекции
                if (defName.Contains("ReactiveCollection") || typeName.Contains("ReactiveCollection"))
                    return string.Empty;

                // UniRx ReactiveProperty<T>
                if ((defName.Contains("ReactiveProperty") || typeName.Contains("ReactiveProperty")) && named.TypeArguments.Length > 0)
                {
                    var arg = named.TypeArguments[0];
                    if (arg == null || arg.Kind == SymbolKind.ErrorType) return string.Empty;
                    isReactive = true;
                    return arg.ToDisplayString();
                }

                // Кастомные реактивные (Vector3ReactiveProperty и т.д.)
                if (typeName.EndsWith("ReactiveProperty"))
                {
                    var valueProp = type.GetMembers("Value").OfType<IPropertySymbol>().FirstOrDefault();
                    if (valueProp?.Type == null || valueProp.Type.Kind == SymbolKind.ErrorType) return string.Empty;
                    isReactive = true;
                    return valueProp.Type.ToDisplayString();
                }
            }

            return type.ToDisplayString();
        }
    }
}