// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Content;

/// <summary>Collects the colorants the content paints, as the interpreter reaches each fill, stroke, glyph, image and shading.</summary>
internal interface IColorantSink
{
    /// <summary>Reports a paint.</summary>
    /// <param name="space">The colour space of the paint.</param>
    /// <param name="components">The first four components of the colour.</param>
    /// <param name="exact">Whether the components are the colour painted; false for an image, which paints every colorant of its space.</param>
    void Painted(PdfColorSpace space, Vector4 components, bool exact);
}
