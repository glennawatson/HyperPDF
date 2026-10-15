// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>How the lines of a paragraph sit between its edges; the values of the Layout <c>/TextAlign</c> attribute (ISO 32000-2 14.8.5.4).</summary>
public enum PdfTextAlignment
{
    /// <summary>Lines start at the start edge, and their ends are ragged.</summary>
    Start = 0,

    /// <summary>Lines are centred between the edges.</summary>
    Center = 1,

    /// <summary>Lines end at the end edge, and their starts are ragged.</summary>
    End = 2,

    /// <summary>Lines reach both edges, except the last, which starts at the start edge.</summary>
    Justify = 3,
}
