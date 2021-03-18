using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace SourceGen
{
    [Generator]
    public class HelloWorld2Generator : ISourceGenerator, ISyntaxReceiver
    {
        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("HelloWorld.cs", @"
using System;
public static class HelloWorld
{
    public static void Hola() => Console.WriteLine(""Hola!"");
}
");
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => this);
        }

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
        }
    }
}
