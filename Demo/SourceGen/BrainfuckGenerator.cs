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
    public class BrainfuckGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var bfFiles = context.AdditionalFiles.Where(o => o.Path.EndsWith(".bf")).ToList();

            if (!bfFiles.Any())
                return;

            var sb = new StringBuilder();
            sb.AppendLine($"public static class BF {{");
            foreach(var file in bfFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file.Path);
                sb.AppendLine($"public static string {name}(string input = default) {{");
                sb.AppendLine($"input ??= string.Empty;");
                sb.AppendLine($"var data = new byte[256];");
                sb.AppendLine($"var dataPointer = 0;");
                sb.AppendLine($"var inputPointer = 0;");
                sb.AppendLine($"var output = string.Empty;");
                sb.AppendLine();

                var content = file.GetText().ToString();
                for(var i = 0; i < content.Length; i++)
                {
                    var c = content[i];
                    switch(c)
                    {
                        case '+':
                            sb.AppendLine($"data[dataPointer]++;");
                            break;
                        case '-':
                            sb.AppendLine($"data[dataPointer]--;");
                            break;
                        case '>':
                            sb.AppendLine($"dataPointer++;");
                            break;
                        case '<':
                            sb.AppendLine($"dataPointer--;");
                            break;
                        case '.':
                            sb.AppendLine($"output += (char)data[dataPointer];");
                            break;
                        case ',':
                            sb.AppendLine($"data[dataPointer] = input[inputPointer++];");
                            break;
                        case '[':
                            sb.AppendLine($"while(data[dataPointer] != 0) {{");
                            break;
                        case ']':
                            sb.AppendLine($"}}");
                            break;
                    }
                }

                sb.AppendLine();
                sb.AppendLine($"return output;");
                sb.AppendLine($"}}");

            }
            sb.AppendLine($"}}");

            var source = SourceText.From(sb.ToString(), Encoding.UTF8);
            context.AddSource("BF.cs", source);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }
    }
}
