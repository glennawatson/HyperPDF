// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>A veraPDF validation profile whose rules HyperPdf maps to its own checks.</summary>
public enum PdfStandardsProfile
{
    /// <summary>The universal accessibility profile of ISO 14289 part 1, edition 2014.</summary>
    PdfUa1 = 0,

    /// <summary>The universal accessibility profile of ISO 14289 part 2, edition 2024.</summary>
    PdfUa2 = 1,

    /// <summary>The tagged PDF structure rules of ISO 32000-1:2008 (Annex L parent-child table).</summary>
    TaggedIso32000Part1 = 2,

    /// <summary>The tagged PDF structure rules of ISO 32000-2:2020 (Annex L parent-child table).</summary>
    TaggedIso32000Part2 = 3,

    /// <summary>The tagged PDF structure rules of ISO/TS 32005:2023, including its Table 5 parent-child pairs.</summary>
    Iso32005 = 4,

    /// <summary>Well-Tagged PDF 1.0, the reuse level.</summary>
    WtpdfReuse = 5,

    /// <summary>Well-Tagged PDF 1.0, the accessibility level.</summary>
    WtpdfAccessibility = 6,

    /// <summary>The WCAG 2.2 PDF profile.</summary>
    Wcag22 = 7,

    /// <summary>The WCAG 2.2 development profile.</summary>
    Wcag22Development = 8,

    /// <summary>The WCAG 2.2 profile of rules a machine can decide.</summary>
    Wcag22Machine = 9,

    /// <summary>The WCAG 2.2 machine-decidable profile for PDF 2.0 files.</summary>
    Wcag22MachinePdf20 = 10,

    /// <summary>The WCAG 2.2 profile with every rule, including those that need a person to judge.</summary>
    Wcag22Complete = 11,

    /// <summary>The complete WCAG 2.2 profile for PDF 2.0 files.</summary>
    Wcag22CompletePdf20 = 12,
}
