// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>How a page drawn on another page is placed and sized. The pages are compared as displayed, after rotation and crop.</summary>
public enum PdfOverlayFit
{
    /// <summary>At its physical size, with its lower left corner on the target's lower left corner.</summary>
    None = 0,

    /// <summary>At its physical size, centred on the target.</summary>
    Center = 1,

    /// <summary>Enlarged or shrunk, keeping its proportions, to fill as much of the target as fits, centred.</summary>
    Fit = 2,
}
