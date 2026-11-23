// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Engine;

/// <summary>A picture read from a file, as tightly packed BGRA pixels with straight alpha, in rows from top to bottom.</summary>
/// <param name="Pixels">The pixels, four bytes each.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
[DebuggerDisplay("HyperPdfPicture: {Width} x {Height}")]
public sealed record HyperPdfPicture(byte[] Pixels, int Width, int Height);
