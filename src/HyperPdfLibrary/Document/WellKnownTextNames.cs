// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Reads the name of a coordinate system from its well-known text (ISO 19162), such as <c>PROJCS["World_Mercator",...]</c>.</summary>
internal static class WellKnownTextNames
{
    /// <summary>The quote that opens and closes a WKT name.</summary>
    private const char Quote = '"';

    /// <summary>A doubled quote, which stands for one quote inside a WKT name.</summary>
    private const string DoubledQuote = "\"\"";

    /// <summary>Reads the quoted name that follows the first keyword's opening bracket.</summary>
    /// <param name="text">The well-known text, or null.</param>
    /// <returns>The name, or null when the text has no quoted name after its first bracket.</returns>
    internal static string? Read(string? text)
    {
        var span = text.AsSpan();
        var open = span.IndexOfAny('[', '(');
        if (open < 0)
        {
            return null;
        }

        var rest = span[(open + 1)..].TrimStart();
        if (rest.IsEmpty || rest[0] != Quote)
        {
            return null;
        }

        rest = rest[1..];
        var end = WellKnownTextNames.FindClosingQuote(rest);
        if (end < 0)
        {
            return null;
        }

        var name = rest[..end].ToString();
        return name.Contains(DoubledQuote, StringComparison.Ordinal) ? name.Replace(DoubledQuote, "\"", StringComparison.Ordinal) : name;
    }

    /// <summary>Finds the quote that closes a name, stepping over doubled quotes.</summary>
    /// <param name="name">The text after the opening quote.</param>
    /// <returns>The closing quote's position, or -1 when the name is not closed.</returns>
    private static int FindClosingQuote(ReadOnlySpan<char> name)
    {
        var end = 0;
        while (end < name.Length)
        {
            var found = name[end..].IndexOf(Quote);
            if (found < 0)
            {
                return -1;
            }

            end += found;
            if (end + 1 >= name.Length || name[end + 1] != Quote)
            {
                return end;
            }

            end += DoubledQuote.Length;
        }

        return -1;
    }
}
