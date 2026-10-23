// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>The screen a GIF's frames are drawn on.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Table">The global colour table: red, green and blue bytes for each entry; empty when there is none.</param>
/// <param name="Transparent">The index of the transparent colour, or -1.</param>
[DebuggerDisplay("GifScreen: {Width}x{Height}")]
internal readonly record struct GifScreen(int Width, int Height, byte[] Table, int Transparent);
