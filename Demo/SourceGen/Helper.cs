using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace SourceGen
{
    [DebuggerStepThrough]
    internal static class Helper
    {
        public static string Capitalize(this string s)
        {
            var charArray = s.ToCharArray();
            if (charArray.Length > 0)
                charArray[0] = char.ToUpperInvariant(charArray[0]);
            return new string(charArray);
        }

        public static void Foreach<T>(this IEnumerable<T> sequence, Action<T> apply)
        {
            foreach (var item in sequence)
                apply(item);
        }

        public static ISymbol GetSymbol(this SemanticModel semanticModel, SyntaxNode syntaxNode, CancellationToken cancellationToken = default)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(syntaxNode, cancellationToken);
            return symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
        }

        public static T GetSymbol<T>(this SemanticModel semanticModel, SyntaxNode syntaxNode, CancellationToken cancellationToken = default)
            where T : class, ISymbol
            => semanticModel.GetSymbol(syntaxNode, cancellationToken) as T;
    }
}

namespace System.Runtime.CompilerServices
{
    internal class IsExternalInit { }
}