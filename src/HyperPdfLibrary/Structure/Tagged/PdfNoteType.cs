// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>The FENote subtype defined by PDF/UA-2 and WTPDF.</summary>
public enum PdfNoteType
{
    /// <summary>The subtype is not specified.</summary>
    None = 0,

    /// <summary>A footnote.</summary>
    Footnote = 1,

    /// <summary>An endnote.</summary>
    Endnote = 2,
}
