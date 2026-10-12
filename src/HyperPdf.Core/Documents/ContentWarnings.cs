// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdf.Core.Documents;

/// <summary>Puts unsupported content into plain words, and spots PDF portfolios from their cover page.</summary>
public static class ContentWarnings
{
    /// <summary>The words Acrobat's portfolio cover pages use.</summary>
    private const string PortfolioWord = "portfolio";

    /// <summary>Every unsupported part's bit.</summary>
    private const int AllParts = (int)(UnsupportedContent.XfaForm | UnsupportedContent.JavaScript | UnsupportedContent.Multimedia | UnsupportedContent.ThreeD | UnsupportedContent.Portfolio);

    /// <summary>The message for each combination of parts, built on first use.</summary>
    private static readonly string?[] Messages = new string?[AllParts + 1];

    /// <summary>The parts, in the order they are listed.</summary>
    private static readonly (UnsupportedContent Part, string Words)[] Parts =
    [
        (UnsupportedContent.XfaForm, "an XFA form"),
        (UnsupportedContent.JavaScript, "form scripts"),
        (UnsupportedContent.Multimedia, "sound or video"),
        (UnsupportedContent.ThreeD, "3D models"),
        (UnsupportedContent.Portfolio, "a PDF portfolio"),
    ];

    /// <summary>Describes what cannot be shown, and what to do about it.</summary>
    /// <param name="content">The unsupported parts.</param>
    /// <returns>The message, or <see langword="null"/> when everything can be shown.</returns>
    public static string? Describe(UnsupportedContent content)
    {
        // Each combination's message is built once; documents opened later reuse it.
        var key = (int)content & AllParts;
        return key == 0 ? null : Messages[key] ??= Build((UnsupportedContent)key);
    }

    /// <summary>Determines whether a document looks like a PDF portfolio: attached files behind a cover page that says so.</summary>
    /// <param name="attachmentCount">The attached files.</param>
    /// <param name="firstPageText">The first page's text.</param>
    /// <returns><see langword="true"/> for a portfolio.</returns>
    public static bool LooksLikePortfolio(int attachmentCount, string firstPageText) =>
        attachmentCount > 0 && firstPageText.Contains(PortfolioWord, StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds the message for a combination of unsupported parts.</summary>
    /// <param name="content">The unsupported parts.</param>
    /// <returns>The message.</returns>
    private static string Build(UnsupportedContent content)
    {
        var names = new List<string>(Parts.Length);
        foreach (var (part, words) in Parts)
        {
            if ((content & part) != 0)
            {
                names.Add(words);
            }
        }

        var builder = new StringBuilder("This document uses ");
        _ = builder.Append(Join(names)).Append(", which HyperPDF cannot show or run. ");
        if ((content & UnsupportedContent.XfaForm) != 0)
        {
            _ = builder.Append("The pages shown are the version made for other viewers and may be empty or out of date. ");
        }

        if ((content & UnsupportedContent.Portfolio) != 0)
        {
            _ = builder.Append("Its files are listed under Attachments. ");
        }

        return builder.Append("To use everything, open it in Adobe Acrobat Reader.").ToString();
    }

    /// <summary>Joins names as "a", "a and b" or "a, b and c".</summary>
    /// <param name="names">The names.</param>
    /// <returns>The joined names.</returns>
    private static string Join(List<string> names) => names.Count == 1
        ? names[0]
        : $"{string.Join(", ", names.GetRange(0, names.Count - 1))} and {names[^1]}";
}
