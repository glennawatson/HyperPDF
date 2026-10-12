// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The kinds of font dictionary a text-showing operator can select, as the PDF/A font rules see them.</summary>
internal enum PdfAFontKind
{
    /// <summary>A Type 1 or multiple master font.</summary>
    Type1 = 0,

    /// <summary>A TrueType font.</summary>
    TrueType = 1,

    /// <summary>A Type 3 font.</summary>
    Type3 = 2,

    /// <summary>A composite (Type 0) font.</summary>
    Type0 = 3,
}
