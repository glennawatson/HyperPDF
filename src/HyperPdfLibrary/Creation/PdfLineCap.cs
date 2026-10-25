// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>How the open ends of a stroked line are finished (ISO 32000-2 8.4.3.3).</summary>
public enum PdfLineCap
{
    /// <summary>The line stops square at its end point.</summary>
    Butt = 0,

    /// <summary>The line ends in a half circle around its end point.</summary>
    Round = 1,

    /// <summary>The line goes on for half its width past its end point.</summary>
    Square = 2,
}
