using Microsoft.CodeAnalysis;
using System;

namespace SourceGen
{
    [Generator]
    public class HelloWorldGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("HelloWorld.cs", @"
using System;

namespace HelloWorldNamespace
{
    public static class HelloWorld
    {
        public static void OnConsole()
        {
            Console.WriteLine(""Hello World."");
        }
    }
}
");
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }
    }
}