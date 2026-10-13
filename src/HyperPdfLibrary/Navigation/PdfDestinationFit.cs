// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Navigation;

/// <summary>How a destination fits its page in the window (ISO 32000-2 12.3.2.2, Table 149).</summary>
public enum PdfDestinationFit
{
    /// <summary>Show the page at a left and top position and a zoom (<c>/XYZ</c>); a missing value keeps the current one.</summary>
    Xyz = 0,

    /// <summary>Fit the whole page in the window (<c>/Fit</c>).</summary>
    Fit = 1,

    /// <summary>Fit the page width, with a top position (<c>/FitH</c>).</summary>
    FitWidth = 2,

    /// <summary>Fit the page height, with a left position (<c>/FitV</c>).</summary>
    FitHeight = 3,

    /// <summary>Fit a rectangle (<c>/FitR</c>).</summary>
    FitRectangle = 4,

    /// <summary>Fit the page's bounding box (<c>/FitB</c>).</summary>
    FitBounds = 5,

    /// <summary>Fit the bounding box's width, with a top position (<c>/FitBH</c>).</summary>
    FitBoundsWidth = 6,

    /// <summary>Fit the bounding box's height, with a left position (<c>/FitBV</c>).</summary>
    FitBoundsHeight = 7,
}
