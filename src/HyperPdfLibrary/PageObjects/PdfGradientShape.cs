// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.PageObjects;

/// <summary>The shape a gradient's colours run along.</summary>
public enum PdfGradientShape
{
    /// <summary>Along a straight line (an axial shading).</summary>
    Linear = 0,

    /// <summary>Outward from a point in circles (a radial shading).</summary>
    Radial = 1,
}
