using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SourceGen
{
    [Generator]
    public class SchemaGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var file = context.AdditionalFiles.FirstOrDefault(o => Path.GetFileName(o.Path) == "schema.txt");
            if (file is null)
                return;

            var fields = file.GetText().Lines
                .Select(MapLine)
                .Where(o => !string.IsNullOrWhiteSpace(o.Type) && !string.IsNullOrWhiteSpace(o.Name))
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"public class Schema {{");

            foreach(var field in fields)
            {
                sb.AppendLine($"{field.Type} {field.Name} {{ get; set; }}");
            }

            sb.AppendLine($"}}");
            var source = SourceText.From(sb.ToString(), Encoding.UTF8);
            context.AddSource("Schema.cs", source);

            (string Type, string Name) MapLine(TextLine line)
            {
                try
                {
                    var splits = line.ToString().Split(':');
                    var type = splits[0];
                    var name = splits[1];
                    return (type, name);
                }
                catch { }
                return (default, default);
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }
    }
}
