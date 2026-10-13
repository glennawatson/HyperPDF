// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>An image to draw as a watermark.</summary>
/// <param name="Pixels">The image as straight-alpha BGRA pixels, rows top to bottom.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="WidthPoints">The width the image is drawn at, in points; the height keeps the image's proportions.</param>
[DebuggerDisplay("PdfWatermarkBitmap: {Width}x{Height} at {WidthPoints} pt")]
public readonly record struct PdfWatermarkBitmap(ReadOnlyMemory<byte> Pixels, int Width, int Height, float WidthPoints);
