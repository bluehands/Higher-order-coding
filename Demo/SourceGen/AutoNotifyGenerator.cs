using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace SourceGen
{
    [Generator]
    public class AutoNotifyGenerator : ISourceGenerator, ISyntaxReceiver
    {
        private const string ATTRIBUTE_CODE = @"
using System;

namespace DarkLink.AutoNotify
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class AutoNotifyAttribute : Attribute
    {
        public AutoNotifyAttribute()
        {
        }

        public bool UsePrivateSetter { get; set; }
    }
}";

        private readonly List<AttributeSyntax> attributeNodeCandidates = new();

        public void Execute(GeneratorExecutionContext context)
        {
            var attributeSource = GetAttributeSource();
            context.AddSource("Attribute.cs", attributeSource);

            if (attributeNodeCandidates.Any())
            {
                var compilation = AddAttributeCompilation(attributeSource, context.Compilation);
                var attributeSymbol = compilation.GetTypeByMetadataName("DarkLink.AutoNotify.AutoNotifyAttribute");

#pragma warning disable RS1024 // Compare symbols correctly
                var classInfoDictionary = new Dictionary<INamedTypeSymbol, ClassInfo>(SymbolEqualityComparer.Default);
#pragma warning restore RS1024 // Compare symbols correctly
                foreach (var attributeNode in attributeNodeCandidates)
                {
                    var semanticModel = compilation.GetSemanticModel(attributeNode.SyntaxTree);
                    var currentAttributeSymbol = semanticModel.GetSymbol<IMethodSymbol>(attributeNode, context.CancellationToken)?.ContainingType;

                    if (!SymbolEqualityComparer.Default.Equals(attributeSymbol, currentAttributeSymbol))
                        continue;

                    if (attributeNode is not
                        {
                            Parent:
                            {
                                Parent: FieldDeclarationSyntax
                                {
                                    Parent: ClassDeclarationSyntax classDeclarationSyntax,
                                } fieldDeclarationSyntax,
                            },
                        })
                    {
                        context.ReportDiagnostic(Diagnostic.Create("DL.AN01", "Generation", "Attribute is not applied to field of class.", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0, false, location: attributeNode.GetLocation()));
                        continue;
                    }

                    if (!classDeclarationSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                    {
                        context.ReportDiagnostic(Diagnostic.Create("DL.AN02", "Generation", "Class is not marked as partial.", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0, false, location: attributeNode.GetLocation()));
                        continue;
                    }

                    if (fieldDeclarationSyntax.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
                    {
                        context.ReportDiagnostic(Diagnostic.Create("DL.AN03", "Generation", "Field is marked as readonly.", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0, false, location: attributeNode.GetLocation()));
                        continue;
                    }

                    var classSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax, context.CancellationToken);

                    if (!classInfoDictionary.TryGetValue(classSymbol, out var classInfo))
                    {
                        classInfo = new ClassInfo(classSymbol);
                        classInfoDictionary[classSymbol] = classInfo;
                    }

                    fieldDeclarationSyntax.Declaration.Variables
                        .Select(variable => FieldInfo.FromFieldSymbol(semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol, attributeSymbol))
                        .Foreach(classInfo.FieldSymbols.Add);
                }

                foreach (var classInfo in classInfoDictionary.Values)
                {
                    var sb = new StringBuilder();

                    sb.AppendLine("using System.ComponentModel;");
                    sb.AppendLine($"namespace {classInfo.TypeSymbol.ContainingNamespace} {{");

                    sb.AppendLine($"    partial class {classInfo.TypeSymbol.Name} : INotifyPropertyChanged {{");
                    sb.AppendLine("        public event PropertyChangedEventHandler PropertyChanged;");

                    foreach (var fieldInfo in classInfo.FieldSymbols)
                    {
                        var fieldSymbol = fieldInfo.FieldSymbol;

                        sb.AppendLine($"        public {fieldSymbol.Type} {fieldInfo.PropertyName} {{");
                        sb.AppendLine($"            get => this.{fieldSymbol.Name};");
                        sb.AppendLine($"            {(fieldInfo.UsePrivateSetter ? "private " : string.Empty)}set {{");
                        sb.AppendLine($"                if(object.Equals(this.{fieldSymbol.Name}, value))");
                        sb.AppendLine("                    return;");
                        sb.AppendLine($"                this.{fieldSymbol.Name} = value;");
                        sb.AppendLine($"                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(\"{fieldInfo.PropertyName}\"));");
                        sb.AppendLine("            }");
                        sb.AppendLine("        }");
                    }

                    sb.AppendLine("    }");

                    sb.AppendLine("}");

                    var source = SourceText.From(sb.ToString(), Encoding.UTF8);
                    context.AddSource($"{classInfo.TypeSymbol}.cs", source);
                }
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            attributeNodeCandidates.Clear();
            context.RegisterForSyntaxNotifications(() => this);
        }

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is AttributeSyntax attributeSyntax && attributeSyntax.Name.ToString().Contains("AutoNotify"))
                attributeNodeCandidates.Add(attributeSyntax);
        }

        private Compilation AddAttributeCompilation(SourceText attributeSource, Compilation compilation)
        {
            var options = (compilation as CSharpCompilation)?.SyntaxTrees[0].Options as CSharpParseOptions;
            return compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(attributeSource, options));
        }

        private SourceText GetAttributeSource() => SourceText.From(ATTRIBUTE_CODE, Encoding.UTF8);
    }

    internal class ClassInfo
    {
        public ClassInfo(INamedTypeSymbol typeSymbol)
        {
            TypeSymbol = typeSymbol;
        }

        public List<FieldInfo> FieldSymbols { get; } = new();

        public INamedTypeSymbol TypeSymbol { get; }
    }

    internal class FieldInfo
    {
        public FieldInfo(IFieldSymbol fieldSymbol, bool usePrivateSetter)
        {
            FieldSymbol = fieldSymbol;
            UsePrivateSetter = usePrivateSetter;
        }

        public IFieldSymbol FieldSymbol { get; }

        public string PropertyName => FieldSymbol.Name.Capitalize();

        public bool UsePrivateSetter { get; }

        public static FieldInfo FromFieldSymbol(IFieldSymbol fieldSymbol, INamedTypeSymbol attributeTypeSymbol)
        {
            var attributeData = fieldSymbol.GetAttributes().First(o => SymbolEqualityComparer.Default.Equals(o.AttributeClass, attributeTypeSymbol));
            var namedArgs = attributeData.NamedArguments.ToDictionary(o => o.Key, o => o.Value);

            var usePrivateSetter = GetNamedArg("UsePrivateSetter", false);

            return new FieldInfo(fieldSymbol, usePrivateSetter);

            T GetNamedArg<T>(string argName, T defaultValue)
            {
                if (namedArgs.TryGetValue(argName, out var typedConstant))
                    return (T)typedConstant.Value;
                return defaultValue;
            }
        }
    }
}