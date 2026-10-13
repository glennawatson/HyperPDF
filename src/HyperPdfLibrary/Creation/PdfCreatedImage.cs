// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Creation;

/// <summary>An image added to a document, ready to draw on any number of pages.</summary>
/// <param name="Id">The image XObject's object.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
[DebuggerDisplay("PdfCreatedImage: {Width}x{Height}")]
public readonly record struct PdfCreatedImage(PdfObjectId Id, int Width, int Height)
{
    /// <summary>Gets the height divided by the width.</summary>
    public float AspectRatio => Width == 0 ? 0F : (float)Height / Width;
}
