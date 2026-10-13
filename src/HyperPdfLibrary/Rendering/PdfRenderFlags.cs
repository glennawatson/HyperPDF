// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>Options for rendering a page.</summary>
[Flags]
public enum PdfRenderFlags
{
    /// <summary>Page content only.</summary>
    None = 0,

    /// <summary>Draw annotation appearances and form widgets.</summary>
    Annotations = 1 << 0,

    /// <summary>Render in grayscale.</summary>
    Grayscale = 1 << 1,

    /// <summary>Render for printing: annotations without the Print flag are left out.</summary>
    Printing = 1 << 2,

    /// <summary>
    /// Convert DeviceCMYK, and DeviceGray and DeviceRGB when the profile matches, through the document's PDF/A or PDF/X
    /// output intent profile, in vector colours, images and shadings. Documents that claim PDF/A and have a usable intent
    /// do this without the flag, as ISO 19005 asks of a reader; the flag adds it for PDF/X and other documents.
    /// </summary>
    OutputIntent = 1 << 3,
}
