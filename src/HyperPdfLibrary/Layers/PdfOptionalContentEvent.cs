// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Layers;

/// <summary>The situation a usage application dictionary applies to (ISO 32000-2, 8.11.4.4, Table 101).</summary>
public enum PdfOptionalContentEvent
{
    /// <summary>The document is shown on screen.</summary>
    View = 0,

    /// <summary>The document is printed.</summary>
    Print = 1,

    /// <summary>The document is saved to a format that has no optional content, such as a raster image.</summary>
    Export = 2,
}
