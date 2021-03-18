using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SourceGen
{
    [Generator]
    public class EnumMatcherGenerator : ISourceGenerator, ISyntaxReceiver
    {
        private record EnumInfo(INamedTypeSymbol EnumTypeSymbol, IReadOnlyList<IFieldSymbol> Fields, bool IsFlags);

        private readonly List<MemberAccessExpressionSyntax> matchAccessNodes = new();

        public void Execute(GeneratorExecutionContext context)
        {
            if (!matchAccessNodes.Any())
                return;

            var flagsAttributeSymbol = context.Compilation.GetTypeByMetadataName("System.FlagsAttribute");
            var enumTypeSymbols = CollectEnumTypes(context.Compilation);
            var enumInfos = enumTypeSymbols.Select(o => MapEnumType(o, flagsAttributeSymbol));
            enumInfos.Foreach(enumInfo => GenerateMatcher(enumInfo, context));
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            matchAccessNodes.Clear();
            context.RegisterForSyntaxNotifications(() => this);

            // Initialize generator
        }

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // Collect relevant syntax nodes
            if (syntaxNode is MemberAccessExpressionSyntax { Name: { Identifier: { Text: "Match" } } } memberAccessExpressionSyntax)
                matchAccessNodes.Add(memberAccessExpressionSyntax);
        }

        private IEnumerable<INamedTypeSymbol> CollectEnumTypes(Compilation compilation)
        {
#pragma warning disable RS1024 // Compare symbols correctly
            var enumTypeSymbols = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
#pragma warning restore RS1024 // Compare symbols correctly

            foreach (var matchAccessNode in matchAccessNodes)
            {
                var semanticModel = compilation.GetSemanticModel(matchAccessNode.SyntaxTree);

                var enumTypeSymbol = semanticModel.GetTypeInfo(matchAccessNode.Expression).Type as INamedTypeSymbol;
                enumTypeSymbols.Add(enumTypeSymbol);
            }

            return enumTypeSymbols;
        }

        private void GenerateFlagsMatcher(EnumInfo enumInfo, StringBuilder sb)
        {
            // Generate Action
            sb.AppendLine($"        public static void Match(");
            sb.Append($"            this {enumInfo.EnumTypeSymbol} thisEnum");
            foreach (var field in enumInfo.Fields)
            {
                sb.AppendLine($",");
                sb.Append($"            Action on{field.Name}");
            }
            sb.AppendLine($") {{");
            foreach (var field in enumInfo.Fields)
            {
                var enumValue = (int)field.ConstantValue;
                sb.AppendLine($"            if (((int)thisEnum & {enumValue}) == {enumValue}) {{");
                sb.AppendLine($"                on{field.Name}();");
                sb.AppendLine($"            }}");
            }
            sb.AppendLine($"        }}");

            // Generate Func
            sb.AppendLine($"        public static IEnumerable<T> Match<T>(");
            sb.Append($"            this {enumInfo.EnumTypeSymbol} thisEnum");
            foreach (var field in enumInfo.Fields)
            {
                sb.AppendLine($",");
                sb.Append($"            Func<T> on{field.Name}");
            }
            sb.AppendLine($") {{");
            foreach (var field in enumInfo.Fields)
            {
                var enumValue = (int)field.ConstantValue;
                sb.AppendLine($"            if (((int)thisEnum & {enumValue}) == {enumValue}) {{");
                sb.AppendLine($"                yield return on{field.Name}();");
                sb.AppendLine($"            }}");
            }
            sb.AppendLine($"        }}");
        }

        private void GenerateMatcher(EnumInfo enumInfo, GeneratorExecutionContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"using System;");
            sb.AppendLine($"using System.Collections.Generic;");
            sb.AppendLine($"namespace {enumInfo.EnumTypeSymbol.ContainingNamespace} {{");
            sb.AppendLine($"    internal static class {enumInfo.EnumTypeSymbol.Name}Matcher {{");

            if (!enumInfo.IsFlags)
                GenerateNormalMatcher(enumInfo, sb);
            else
                GenerateFlagsMatcher(enumInfo, sb);

            sb.AppendLine($"    }}");
            sb.AppendLine($"}}");

            var sourceText = SourceText.From(sb.ToString(), Encoding.UTF8);
            context.AddSource($"{enumInfo.EnumTypeSymbol}.cs", sourceText);
        }

        private void GenerateNormalMatcher(EnumInfo enumInfo, StringBuilder sb)
        {
            // Generate Action
            sb.AppendLine($"        public static void Match(");
            sb.Append($"            this {enumInfo.EnumTypeSymbol} thisEnum");
            foreach (var field in enumInfo.Fields)
            {
                sb.AppendLine($",");
                sb.Append($"            Action on{field.Name}");
            }
            sb.AppendLine($") {{");
            sb.AppendLine($"            switch(thisEnum) {{");
            foreach (var field in enumInfo.Fields)
            {
                sb.AppendLine($"                case {field}:");
                sb.AppendLine($"                    on{field.Name}();");
                sb.AppendLine($"                    break;");
            }
            sb.AppendLine($"                default:");
            sb.AppendLine($"                    throw new NotSupportedException();");
            sb.AppendLine($"            }}");
            sb.AppendLine($"        }}");

            // Generate Func
            sb.AppendLine($"        public static T Match<T>(");
            sb.Append($"            this {enumInfo.EnumTypeSymbol} thisEnum");
            foreach (var field in enumInfo.Fields)
            {
                sb.AppendLine($",");
                sb.Append($"            Func<T> on{field.Name}");
            }
            sb.AppendLine($") {{");
            sb.AppendLine($"            switch(thisEnum) {{");
            foreach (var field in enumInfo.Fields)
            {
                sb.AppendLine($"                case {field}:");
                sb.AppendLine($"                    return on{field.Name}();");
            }
            sb.AppendLine($"                default:");
            sb.AppendLine($"                    throw new NotSupportedException();");
            sb.AppendLine($"            }}");
            sb.AppendLine($"        }}");
        }

        private EnumInfo MapEnumType(INamedTypeSymbol enumTypeSymbol, INamedTypeSymbol flagsAttributeTypeSymbol)
        {
            var fields = enumTypeSymbol.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(o => o.IsStatic && o.IsConst);
            var isFlags = enumTypeSymbol.GetAttributes().Any(o => SymbolEqualityComparer.Default.Equals(o.AttributeClass, flagsAttributeTypeSymbol));
            return new EnumInfo(enumTypeSymbol, fields.ToList(), isFlags);
        }
    }
}