// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>Splits a form script into tokens. Anything outside the small language it allows is refused.</summary>
internal static class SandboxLexer
{
    /// <summary>The longest script read, in characters.</summary>
    internal const int MaxSourceLength = 20_000;

    /// <summary>The most tokens read from one script.</summary>
    internal const int MaxTokens = 6_000;

    /// <summary>The length of the markers that open and close a comment.</summary>
    private const int CommentMarkerLength = 2;

    /// <summary>The white space the language allows between tokens.</summary>
    private static readonly SearchValues<char> Whitespace = SearchValues.Create(" \t\r\n\f\v");

    /// <summary>The characters a name can start with.</summary>
    private static readonly SearchValues<char> NameStarts = SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_$");

    /// <summary>The characters a name can contain.</summary>
    private static readonly SearchValues<char> NameChars = SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_$");

    /// <summary>The characters an operator or punctuation can start with.</summary>
    private static readonly SearchValues<char> SymbolStarts = SearchValues.Create("+-*/%<>!=&|?:,;.(){}[]");

    /// <summary>The operators and punctuation, the longest first so that the first match is the longest.</summary>
    private static readonly string[] Symbols =
    [
        "===", "!==", "==", "!=", "<=", ">=", "&&", "||", "+=", "-=", "*=", "/=", "++", "--",
        "+", "-", "*", "/", "%", "<", ">", "!", "=", "?", ":", ",", ";", ".", "(", ")", "{", "}", "[", "]",
    ];

    /// <summary>Reads the tokens of a script.</summary>
    /// <param name="source">The script text.</param>
    /// <returns>The tokens, ending with an <see cref="SandboxTokenKind.End"/> token.</returns>
    /// <exception cref="SandboxException">The script is too long, has too many tokens, or uses a character the language does not.</exception>
    internal static List<SandboxToken> Tokenize(string source)
    {
        if (source.Length > MaxSourceLength)
        {
            throw new SandboxException("The script is too long to run.");
        }

        var tokens = new List<SandboxToken>();
        var at = 0;
        while (SkipSpace(source, ref at))
        {
            if (tokens.Count >= MaxTokens)
            {
                throw new SandboxException("The script is too long to run.");
            }

            tokens.Add(ReadToken(source, ref at));
        }

        tokens.Add(new(SandboxTokenKind.End, string.Empty, 0));
        return tokens;
    }

    /// <summary>Skips white space and comments.</summary>
    /// <param name="source">The script text.</param>
    /// <param name="at">The position, moved past what was skipped.</param>
    /// <returns><see langword="true"/> when there is more script.</returns>
    private static bool SkipSpace(string source, ref int at)
    {
        while (true)
        {
            var rest = source.AsSpan(at);
            var spaces = rest.IndexOfAnyExcept(Whitespace);
            at += spaces < 0 ? rest.Length : spaces;
            var after = SkipComment(source, at);
            if (after == at)
            {
                return at < source.Length;
            }

            at = after;
        }
    }

    /// <summary>Skips a comment that starts at a position.</summary>
    /// <param name="source">The script text.</param>
    /// <param name="at">The position.</param>
    /// <returns>The position after the comment, or the same position when no comment starts there.</returns>
    private static int SkipComment(string source, int at)
    {
        var rest = source.AsSpan(at);
        if (rest.StartsWith("//", StringComparison.Ordinal))
        {
            var end = rest.IndexOf('\n');
            return at + (end < 0 ? rest.Length : end);
        }

        if (!rest.StartsWith("/*", StringComparison.Ordinal))
        {
            return at;
        }

        var close = rest[CommentMarkerLength..].IndexOf("*/", StringComparison.Ordinal);
        return close < 0 ? source.Length : at + CommentMarkerLength + close + CommentMarkerLength;
    }

    /// <summary>Reads one token.</summary>
    /// <param name="source">The script text.</param>
    /// <param name="at">The position, moved past the token.</param>
    /// <returns>The token.</returns>
    private static SandboxToken ReadToken(string source, ref int at)
    {
        var c = source[at];
        var startsNumber = char.IsAsciiDigit(c) || (c == '.' && at + 1 < source.Length && char.IsAsciiDigit(source[at + 1]));
        if (startsNumber)
        {
            return ReadNumber(source, ref at);
        }

        if (c is '"' or '\'')
        {
            return ReadString(source, ref at);
        }

        return NameStarts.Contains(c) ? ReadName(source, ref at) : ReadSymbol(source, ref at);
    }

    /// <summary>Reads a name.</summary>
    /// <param name="source">The script text.</param>
    /// <param name="at">The position, moved past the name.</param>
    /// <returns>The token.</returns>
    private static SandboxToken ReadName(string source, ref int at)
    {
        var rest = source.AsSpan(at);
        var length = rest.IndexOfAnyExcept(NameChars);
        length = length < 0 ? rest.Length : length;
        var name = rest[..length].ToString();
        at += length;
        return new(SandboxTokenKind.Name, name, 0);
    }

    /// <summary>Reads a number.</summary>
    /// <param name="source">The script text.</param>
    /// <param name="at">The position, moved past the number.</param>
    /// <returns>The token.</returns>
    /// <exception cref="SandboxException">The number is not well formed.</exception>
    private static SandboxToken ReadNumber(string source, ref int at)
    {
        var start = at;
        SkipDigits(source, ref at);
        if (at < source.Length && source[at] is 'e' or 'E')
        {
            at++;
            at += at < source.Length && source[at] is '+' or '-' ? 1 : 0;
            SkipDigits(source, ref at);
        }

        return double.TryParse(source.AsSpan(start, at - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? new(SandboxTokenKind.Number, string.Empty, number)
            : throw new SandboxException("A number in the script is not well formed.");
    }

    /// <summary>Skips digits and decimal points.</summary>
    /// <param name="source">The script text.</param>
    /// <param name="at">The position, moved past them.</param>
    private static void SkipDigits(string source, ref int at)
    {
        while (at < source.Length && (char.IsAsciiDigit(source[at]) || source[at] == '.'))
        {
            at++;
        }
    }

    /// <summary>Reads a string in single or double quotes.</summary>
    /// <param name="source">The script text.</param>
    /// <param name="at">The position, moved past the closing quote.</param>
    /// <returns>The token.</returns>
    /// <exception cref="SandboxException">The string is not closed.</exception>
    private static SandboxToken ReadString(string source, ref int at)
    {
        var quote = source[at];
        at++;

        // A string with no escape is a slice of the script; only one with escapes needs a builder.
        var plain = source.AsSpan(at).IndexOfAny(quote, '\\');
        if (plain >= 0 && source[at + plain] == quote)
        {
            var text = source[at..(at + plain)];
            at += plain + 1;
            return new(SandboxTokenKind.String, text, 0);
        }

        return ReadEscapedString(source, ref at, quote);
    }

    /// <summary>Reads a string that has escapes, or is not closed.</summary>
    /// <param name="source">The script text.</param>
    /// <param name="at">The position after the opening quote, moved past the closing quote.</param>
    /// <param name="quote">The quote that closes the string.</param>
    /// <returns>The token.</returns>
    /// <exception cref="SandboxException">The string is not closed.</exception>
    private static SandboxToken ReadEscapedString(string source, ref int at, char quote)
    {
        var text = new StringBuilder();
        while (at < source.Length && source[at] != quote)
        {
            var c = source[at];
            at++;
            if (c == '\\' && at < source.Length)
            {
                c = Unescape(source[at]);
                at++;
            }

            _ = text.Append(c);
        }

        if (at >= source.Length)
        {
            throw new SandboxException("A string in the script is not closed.");
        }

        at++;
        return new(SandboxTokenKind.String, text.ToString(), 0);
    }

    /// <summary>Gives the character an escape stands for.</summary>
    /// <param name="escaped">The character after the backslash.</param>
    /// <returns>The character.</returns>
    private static char Unescape(char escaped) => escaped switch
    {
        'n' => '\n',
        'r' => '\r',
        't' => '\t',
        _ => escaped,
    };

    /// <summary>Reads an operator or punctuation, preferring the longest.</summary>
    /// <param name="source">The script text.</param>
    /// <param name="at">The position, moved past the symbol.</param>
    /// <returns>The token.</returns>
    /// <exception cref="SandboxException">The character is not part of the language.</exception>
    private static SandboxToken ReadSymbol(string source, ref int at)
    {
        var rest = source.AsSpan(at);
        var found = SymbolStarts.Contains(rest[0]) ? IndexOfSymbol(rest) : -1;
        return found < 0 ? throw new SandboxException($"The script uses '{rest[0]}', which form scripts do not.") : Take(Symbols[found], ref at);
    }

    /// <summary>Finds the first symbol, which is the longest, that a stretch of script starts with.</summary>
    /// <param name="rest">The script from the current position.</param>
    /// <returns>The index in <see cref="Symbols"/>, or -1.</returns>
    private static int IndexOfSymbol(ReadOnlySpan<char> rest)
    {
        var index = 0;
        while (index < Symbols.Length && !rest.StartsWith(Symbols[index], StringComparison.Ordinal))
        {
            index++;
        }

        return index < Symbols.Length ? index : -1;
    }

    /// <summary>Makes a symbol token and moves past it.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="at">The position, moved past the symbol.</param>
    /// <returns>The token.</returns>
    private static SandboxToken Take(string symbol, ref int at)
    {
        at += symbol.Length;
        return new(SandboxTokenKind.Symbol, symbol, 0);
    }
}
