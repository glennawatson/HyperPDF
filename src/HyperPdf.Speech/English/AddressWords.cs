// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Speech.English;

/// <summary>Says web and e-mail addresses with their dots, slashes and dashes as words.</summary>
internal static class AddressWords
{
    /// <summary>The start of a web address with a secure scheme.</summary>
    private const string SecureScheme = "https://";

    /// <summary>The start of a web address with a plain scheme.</summary>
    private const string PlainScheme = "http://";

    /// <summary>The label many web addresses start with.</summary>
    private const string WorldWideWeb = "www.";

    /// <summary>Writes an e-mail address: "a.b@c.com" as "a dot b at c dot com".</summary>
    /// <param name="context">The address.</param>
    internal static void WriteEmail(in MatchContext context)
    {
        var match = context.Match;
        var at = match.IndexOf('@');
        Say(match[..at], context.Output);
        context.Output.Append(" at ");
        Say(match[(at + 1)..], context.Output);
    }

    /// <summary>Writes a web address without its scheme or leading "www.".</summary>
    /// <param name="context">The address.</param>
    internal static void WriteUrl(in MatchContext context)
    {
        var address = context.Match;
        if (address.StartsWith(SecureScheme))
        {
            address = address[SecureScheme.Length..];
        }
        else if (address.StartsWith(PlainScheme))
        {
            address = address[PlainScheme.Length..];
        }

        // "www." is left out only when a host with its own dot remains.
        if (address.StartsWith(WorldWideWeb) && address[WorldWideWeb.Length..SlashOrEnd(address)].Contains('.'))
        {
            address = address[WorldWideWeb.Length..];
        }

        Say(address, context.Output);
    }

    /// <summary>Finds the first slash.</summary>
    /// <param name="address">The address.</param>
    /// <returns>The slash's index, or the address's length when there is none.</returns>
    private static int SlashOrEnd(ReadOnlySpan<char> address)
    {
        var slash = address.IndexOf('/');
        return slash < 0 ? address.Length : slash;
    }

    /// <summary>Says a web address or e-mail part: dots, slashes and dashes as words.</summary>
    /// <param name="address">The address without its scheme.</param>
    /// <param name="output">Receives the words.</param>
    private static void Say(ReadOnlySpan<char> address, SpokenText output)
    {
        foreach (var c in address.TrimEnd('/'))
        {
            switch (c)
            {
                case '.':
                {
                    output.Append(" dot ");
                    break;
                }

                case '/':
                {
                    output.Append(" slash ");
                    break;
                }

                case '-':
                {
                    output.Append(" dash ");
                    break;
                }

                case '_':
                {
                    output.Append(" underscore ");
                    break;
                }

                default:
                {
                    output.Append(c);
                    break;
                }
            }
        }
    }
}
