// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Annotations;

/// <summary>Describes what happened to a page's trap network in plain words.</summary>
internal static class PdfTrapNetworkText
{
    /// <summary>What a print shop can do about traps that were removed.</summary>
    private const string Advice = "HyperPDF cannot make new traps. Ask your print shop to add traps again before the file is printed on a press.";

    /// <summary>The reasons, in the order the most useful one is chosen.</summary>
    private static readonly Reason[] Reasons =
    [
        new(PdfTrapNetworkProblems.ContentChanged, "the page changed after the traps were made"),
        new(PdfTrapNetworkProblems.AnnotationsChanged, "comments or form fields on the page changed after the traps were made"),
        new(PdfTrapNetworkProblems.PageNewer, "the page is dated later than its traps"),
        new(PdfTrapNetworkProblems.MultipleNetworks, "the page had more than one set of traps, and a page may have only one"),
        new(PdfTrapNetworkProblems.MissingAppearance, "the traps had no drawing to print"),
        new(PdfTrapNetworkProblems.MissingChangeTracking, "the traps did not record what they were made for, so they cannot be checked"),
        new(PdfTrapNetworkProblems.ReferencesLost, "the traps point at parts of the file that cannot be copied"),
    ];

    /// <summary>Describes a change.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="action">What was done.</param>
    /// <param name="problems">What was wrong.</param>
    /// <returns>The description.</returns>
    internal static string Describe(int pageIndex, PdfTrapNetworkAction action, PdfTrapNetworkProblems problems)
    {
        var page = pageIndex + 1;
        var reason = Explain(problems);
        return action switch
        {
            PdfTrapNetworkAction.Removed => string.Create(CultureInfo.InvariantCulture, $"HyperPDF removed the trap network on page {page}, because {reason}. {Advice}"),
            PdfTrapNetworkAction.Repaired => string.Create(
                CultureInfo.InvariantCulture,
                $"HyperPDF fixed how the trap network on page {page} is stored, so it still prints after the page's other marks."),
            PdfTrapNetworkAction.LeftOut => string.Create(CultureInfo.InvariantCulture, $"HyperPDF did not copy the trap network of page {page}, because {reason}. {Advice}"),
            PdfTrapNetworkAction.Unchanged => string.Create(
                CultureInfo.InvariantCulture,
                $"The trap network on page {page} should be removed, because {reason}, but HyperPDF could not change the page. {Advice}"),
            _ => string.Empty,
        };
    }

    /// <summary>Picks the reason to give for a set of problems.</summary>
    /// <param name="problems">The problems.</param>
    /// <returns>The reason.</returns>
    private static string Explain(PdfTrapNetworkProblems problems)
    {
        foreach (var (problem, text) in Reasons)
        {
            if ((problems & problem) != 0)
            {
                return text;
            }
        }

        return "it was not stored the way a PDF file must store it";
    }

    /// <summary>A problem and the words that explain it.</summary>
    /// <param name="Problem">The problem.</param>
    /// <param name="Text">The explanation, which follows "because".</param>
    private readonly record struct Reason(PdfTrapNetworkProblems Problem, string Text);
}
