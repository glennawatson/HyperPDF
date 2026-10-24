// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Chooses which header and footer a page has: the first page's, an even page's or the usual one.</summary>
internal static class RunningContentSelector
{
    /// <summary>Chooses the header of a page.</summary>
    /// <param name="section">The section.</param>
    /// <param name="sectionPage">The page's place in the section, counted from 1.</param>
    /// <param name="displayNumber">The number the page shows.</param>
    /// <returns>The header, or <see langword="null"/> for none.</returns>
    internal static PdfRunningContent? SelectHeader(PdfSection section, int sectionPage, int displayNumber)
    {
        if (section.Setup.DifferentFirstPage && sectionPage == 1)
        {
            return section.FirstPageHeader;
        }

        return section.Setup.DifferentOddAndEven && (displayNumber & 1) == 0 ? section.EvenPageHeader ?? section.Header : section.Header;
    }

    /// <summary>Chooses the footer of a page.</summary>
    /// <param name="section">The section.</param>
    /// <param name="sectionPage">The page's place in the section, counted from 1.</param>
    /// <param name="displayNumber">The number the page shows.</param>
    /// <returns>The footer, or <see langword="null"/> for none.</returns>
    internal static PdfRunningContent? SelectFooter(PdfSection section, int sectionPage, int displayNumber)
    {
        if (section.Setup.DifferentFirstPage && sectionPage == 1)
        {
            return section.FirstPageFooter;
        }

        return section.Setup.DifferentOddAndEven && (displayNumber & 1) == 0 ? section.EvenPageFooter ?? section.Footer : section.Footer;
    }
}
