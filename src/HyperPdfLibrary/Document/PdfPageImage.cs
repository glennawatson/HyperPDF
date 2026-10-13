// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>An image that becomes a page.</summary>
/// <param name="Pixels">The image as straight-alpha BGRA pixels, rows top to bottom.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
[DebuggerDisplay("PdfPageImage: {Width}x{Height}")]
public readonly record struct PdfPageImage(ReadOnlyMemory<byte> Pixels, int Width, int Height);
