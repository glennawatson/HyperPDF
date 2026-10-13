// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Features;

/// <summary>What is wrong with a page's separation dictionary (ISO 32000-2 §14.11.4, Table 400).</summary>
public enum PdfSeparationProblem
{
    /// <summary>No problem.</summary>
    None = 0,

    /// <summary>A <c>/Pages</c> entry is not a reference to a page of the document; it was ignored.</summary>
    NotAPage = 1,

    /// <summary><c>/Pages</c> is missing, is not an array or does not list the page itself; the page is not grouped.</summary>
    MissingPages = 2,

    /// <summary>The page's <c>/Pages</c> array differs from the one of the group it was placed in.</summary>
    PagesMismatch = 3,

    /// <summary><c>/DeviceColorant</c> is missing or is not a name or string.</summary>
    MissingColorant = 4,

    /// <summary><c>/DeviceColorant</c> is not a colourant of the <c>/ColorSpace</c> given.</summary>
    ColorantMismatch = 5,

    /// <summary><c>/ColorSpace</c> is not a Separation or DeviceN colour space array; it was ignored.</summary>
    InvalidColorSpace = 6,

    /// <summary>The page is listed by more than one group; it stays in the first.</summary>
    PageInTwoGroups = 7,

    /// <summary>A page listed in a group has no separation dictionary of its own; it is not grouped.</summary>
    MemberWithoutSeparationInfo = 8,

    /// <summary>A limit on separation pages or <c>/Pages</c> entries was reached and the rest was ignored.</summary>
    TooManyEntries = 9,
}
