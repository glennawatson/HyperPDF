// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Finds web and email addresses written as plain text. White space ends a candidate word. Inside a word:
/// <list type="bullet">
/// <item><description>
/// A web address starts with <c>http://</c>, <c>https://</c>, <c>ftp://</c>, <c>mailto:</c> or <c>www.</c> (any case),
/// at the start of the word or after a character that is not a letter or digit. It runs over the characters RFC 3986
/// allows in a URI and stops at the first other character.
/// </description></item>
/// <item><description>
/// Trailing sentence punctuation (<c>. , ; : ! ? ' "</c>) is not part of the address, and neither is a closing bracket
/// that has no opening bracket inside the address.
/// </description></item>
/// <item><description>
/// A <c>www.</c> address needs a host with a dot after the prefix; the other schemes need a host character after the prefix.
/// </description></item>
/// <item><description>
/// An email address is a local part of RFC 5322 atom characters and dots, an <c>@</c>, and a domain of at least two dot
/// separated labels whose last label is two or more letters.
/// </description></item>
/// </list>
/// </summary>
internal static class TextLinkParser
{
    /// <summary>The prefix of a web address that has no scheme.</summary>
    private const string WebPrefix = "www.";

    /// <summary>The scheme added to an address that has none.</summary>
    private const string DefaultScheme = "http://";

    /// <summary>The scheme of an email address.</summary>
    private const string MailScheme = "mailto:";

    /// <summary>The punctuation that ends a sentence and so is not part of an address ending in it.</summary>
    private const string TrailingPunctuation = ".,;:!?'\"";

    /// <summary>The characters other than letters and digits that RFC 3986 allows in a URI.</summary>
    private const string UriSymbols = "-._~:/?#[]@!$&'()*+,;=%";

    /// <summary>The characters other than letters and digits that RFC 5322 allows in an email local part atom, plus the dot.</summary>
    private const string LocalSymbols = "!#$%&'*+/=?^_`{|}~-.";

    /// <summary>The characters that end the host of a web address.</summary>
    private const string HostEnd = "/?#:";

    /// <summary>Two dots in a row, which no local part or domain may contain.</summary>
    private const string DoubleDot = "..";

    /// <summary>The smallest length of the last label of an email domain.</summary>
    private const int MinTopLabel = 2;

    /// <summary>The schemes that start a web address.</summary>
    private static readonly string[] Schemes = ["http://", "https://", "ftp://", MailScheme];

    /// <summary>The characters RFC 3986 allows in a URI: ASCII letters, digits and the URI symbols.</summary>
    private static readonly SearchValues<char> UriChars = SearchValues.Create($"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789{UriSymbols}");

    /// <summary>The characters that separate words: every character that is white space.</summary>
    private static readonly SearchValues<char> WhiteSpace = SearchValues.Create(CollectWhiteSpace());

    /// <summary>Finds the addresses in a page's text.</summary>
    /// <param name="page">The text page.</param>
    /// <returns>The addresses in page order.</returns>
    internal static PdfWebLink[] Extract(PdfTextPage page)
    {
        var text = page.Text;
        var links = new List<PdfWebLink>();
        var index = 0;
        while (index < text.Length)
        {
            var skip = text.AsSpan(index).IndexOfAnyExcept(WhiteSpace);
            if (skip < 0)
            {
                break;
            }

            index += skip;
            var length = text.AsSpan(index).IndexOfAny(WhiteSpace);
            var end = length < 0 ? text.Length : index + length;
            AddWordLinks(page, text, index, end, links);
            index = end;
        }

        return [.. links];
    }

    /// <summary>Lists the white space characters of the Basic Multilingual Plane.</summary>
    /// <returns>The characters.</returns>
    private static string CollectWhiteSpace()
    {
        var builder = new StringBuilder();
        for (var value = 0; value <= char.MaxValue; value++)
        {
            if (char.IsWhiteSpace((char)value))
            {
                _ = builder.Append((char)value);
            }
        }

        return builder.ToString();
    }

    /// <summary>Finds the addresses inside one word.</summary>
    /// <param name="page">The text page.</param>
    /// <param name="text">The page text.</param>
    /// <param name="start">The word start.</param>
    /// <param name="end">The position after the word.</param>
    /// <param name="links">Receives the addresses.</param>
    private static void AddWordLinks(PdfTextPage page, string text, int start, int end, List<PdfWebLink> links)
    {
        var position = start;
        while (position < end)
        {
            var link = FindWeb(text, position, end) ?? FindMail(text, position, end);
            if (link is not { } found)
            {
                return;
            }

            var first = page.CharIndexFromTextIndex(found.Start);
            var last = page.CharIndexFromTextIndex(found.End - 1);
            links.Add(new(found.Url, first, last - first + 1));
            position = found.End;
        }
    }

    /// <summary>Finds the first web address in a word.</summary>
    /// <param name="text">The page text.</param>
    /// <param name="from">Where to start looking.</param>
    /// <param name="wordEnd">The position after the word.</param>
    /// <returns>The address, or <see langword="null"/>.</returns>
    private static Found? FindWeb(string text, int from, int wordEnd)
    {
        for (var start = from; start < wordEnd; start++)
        {
            if (start > from && char.IsLetterOrDigit(text[start - 1]))
            {
                continue;
            }

            var prefix = PrefixAt(text.AsSpan(start, wordEnd - start));
            if (prefix >= 0 && ExtendWeb(text, start, wordEnd, prefix) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Gets the length of the scheme or <c>www.</c> prefix a word piece starts with.</summary>
    /// <param name="rest">The word from the position to test.</param>
    /// <returns>The prefix length, or -1 for none.</returns>
    private static int PrefixAt(ReadOnlySpan<char> rest)
    {
        foreach (var scheme in Schemes)
        {
            if (rest.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            {
                return scheme.Length;
            }
        }

        return rest.StartsWith(WebPrefix, StringComparison.OrdinalIgnoreCase) ? WebPrefix.Length : -1;
    }

    /// <summary>Extends a prefix over the address it starts and checks the result.</summary>
    /// <param name="text">The page text.</param>
    /// <param name="start">The address start.</param>
    /// <param name="wordEnd">The position after the word.</param>
    /// <param name="prefix">The prefix length.</param>
    /// <returns>The address, or <see langword="null"/> when it is not valid.</returns>
    private static Found? ExtendWeb(string text, int start, int wordEnd, int prefix)
    {
        var tail = text.AsSpan(start + prefix, wordEnd - start - prefix);
        var length = tail.IndexOfAnyExcept(UriChars);
        var end = TrimEnd(text, start, start + prefix + (length < 0 ? tail.Length : length));
        var address = text.AsSpan(start, end - start);
        if (end - start < prefix || !IsValidAddress(address, prefix))
        {
            return null;
        }

        var bare = address.StartsWith(WebPrefix, StringComparison.OrdinalIgnoreCase);
        return new(bare ? DefaultScheme + address.ToString() : address.ToString(), start, end);
    }

    /// <summary>Checks the part of an address after its prefix.</summary>
    /// <param name="address">The address including its prefix.</param>
    /// <param name="prefix">The prefix length.</param>
    /// <returns><see langword="true"/> when the address has a usable host or mailbox.</returns>
    private static bool IsValidAddress(ReadOnlySpan<char> address, int prefix)
    {
        var body = address[prefix..];
        if (address.StartsWith(MailScheme, StringComparison.OrdinalIgnoreCase))
        {
            return IsEmail(body);
        }

        return address.StartsWith(WebPrefix, StringComparison.OrdinalIgnoreCase) ? HasDottedHost(body) : HasHost(body);
    }

    /// <summary>Removes trailing punctuation and unbalanced closing brackets from an address.</summary>
    /// <param name="text">The page text.</param>
    /// <param name="start">The address start.</param>
    /// <param name="end">The position after the address.</param>
    /// <returns>The new end.</returns>
    private static int TrimEnd(string text, int start, int end)
    {
        while (end > start)
        {
            var last = text[end - 1];
            var drop = TrailingPunctuation.Contains(last, StringComparison.Ordinal) || (IsCloser(last) && !IsBalanced(text.AsSpan(start, end - start), last));
            if (!drop)
            {
                break;
            }

            end--;
        }

        return end;
    }

    /// <summary>Determines whether a character closes a bracket.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for <c>)</c>, <c>]</c> and <c>}</c>.</returns>
    private static bool IsCloser(char value) => value is ')' or ']' or '}';

    /// <summary>Determines whether a closing bracket at the end of an address has an opening bracket inside it.</summary>
    /// <param name="address">The address including the closing bracket.</param>
    /// <param name="closer">The closing bracket.</param>
    /// <returns><see langword="true"/> when the opening bracket count covers the closing bracket count.</returns>
    private static bool IsBalanced(ReadOnlySpan<char> address, char closer)
    {
        var opener = closer switch
        {
            ')' => '(',
            ']' => '[',
            _ => '{',
        };

        return address.Count(opener) >= address.Count(closer);
    }

    /// <summary>Determines whether the text after a scheme starts with a host character.</summary>
    /// <param name="body">The text after the scheme.</param>
    /// <returns><see langword="true"/> when the host starts with a letter or digit.</returns>
    private static bool HasHost(ReadOnlySpan<char> body) => !body.IsEmpty && char.IsAsciiLetterOrDigit(body[0]);

    /// <summary>Determines whether the text after <c>www.</c> is a host with at least one more dot.</summary>
    /// <param name="body">The text after the prefix.</param>
    /// <returns><see langword="true"/> when it starts with a label and contains a dot after it.</returns>
    private static bool HasDottedHost(ReadOnlySpan<char> body)
    {
        var stop = body.IndexOfAny(HostEnd);
        var host = stop >= 0 ? body[..stop] : body;
        var dot = host.IndexOf('.');
        return dot > 0 && dot < host.Length - 1 && char.IsAsciiLetterOrDigit(host[0]);
    }

    /// <summary>Finds the first email address in a word.</summary>
    /// <param name="text">The page text.</param>
    /// <param name="from">Where to start looking.</param>
    /// <param name="wordEnd">The position after the word.</param>
    /// <returns>The address, or <see langword="null"/>.</returns>
    private static Found? FindMail(string text, int from, int wordEnd)
    {
        for (var sign = from; sign < wordEnd; sign++)
        {
            if (text[sign] == '@' && MailAround(text, sign, from, wordEnd) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Grows an email address around an at sign and checks it.</summary>
    /// <param name="text">The page text.</param>
    /// <param name="sign">The position of the at sign.</param>
    /// <param name="from">The earliest position the address may start.</param>
    /// <param name="wordEnd">The position after the word.</param>
    /// <returns>The address, or <see langword="null"/>.</returns>
    private static Found? MailAround(string text, int sign, int from, int wordEnd)
    {
        var start = sign;
        while (start > from && IsLocalChar(text[start - 1]))
        {
            start--;
        }

        var end = DomainEnd(text, sign, wordEnd);
        var address = text.AsSpan(start, end - start);
        return IsEmail(address) ? new(MailScheme + address.ToString(), start, end) : null;
    }

    /// <summary>Finds where the domain after an at sign ends, leaving out trailing dots and hyphens.</summary>
    /// <param name="text">The page text.</param>
    /// <param name="sign">The position of the at sign.</param>
    /// <param name="wordEnd">The position after the word.</param>
    /// <returns>The position after the domain.</returns>
    private static int DomainEnd(string text, int sign, int wordEnd)
    {
        var end = sign + 1;
        while (end < wordEnd && (char.IsAsciiLetterOrDigit(text[end]) || text[end] is '-' or '.'))
        {
            end++;
        }

        while (end > sign + 1 && text[end - 1] is '.' or '-')
        {
            end--;
        }

        return end;
    }

    /// <summary>Determines whether a character can be part of an email local part.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for atom characters and dots.</returns>
    private static bool IsLocalChar(char value) => char.IsAsciiLetterOrDigit(value) || LocalSymbols.Contains(value, StringComparison.Ordinal);

    /// <summary>Validates an email address.</summary>
    /// <param name="address">The address.</param>
    /// <returns><see langword="true"/> for a valid local part and domain.</returns>
    private static bool IsEmail(ReadOnlySpan<char> address)
    {
        var at = address.IndexOf('@');
        return at > 0 && IsLocalPart(address[..at]) && IsDomain(address[(at + 1)..]);
    }

    /// <summary>Validates an email local part.</summary>
    /// <param name="local">The text before the at sign.</param>
    /// <returns><see langword="true"/> when it has no leading, trailing or doubled dot.</returns>
    private static bool IsLocalPart(ReadOnlySpan<char> local) => local[0] != '.' && local[^1] != '.' && local.IndexOf(DoubleDot) < 0;

    /// <summary>Validates an email domain.</summary>
    /// <param name="domain">The text after the at sign.</param>
    /// <returns><see langword="true"/> for dot separated labels ending in a label of letters.</returns>
    private static bool IsDomain(ReadOnlySpan<char> domain)
    {
        var dot = domain.LastIndexOf('.');
        return dot > 0 && domain[0] != '-' && domain.IndexOf(DoubleDot) < 0 && IsTopLabel(domain[(dot + 1)..]);
    }

    /// <summary>Validates the last label of an email domain.</summary>
    /// <param name="label">The label.</param>
    /// <returns><see langword="true"/> for two or more ASCII letters.</returns>
    private static bool IsTopLabel(ReadOnlySpan<char> label)
    {
        foreach (var value in label)
        {
            if (!char.IsAsciiLetter(value))
            {
                return false;
            }
        }

        return label.Length >= MinTopLabel;
    }

    /// <summary>An address found in the text.</summary>
    /// <param name="Url">The address as a URI.</param>
    /// <param name="Start">The text index of its first character.</param>
    /// <param name="End">The text index after its last character.</param>
    private readonly record struct Found(string Url, int Start, int End);
}
