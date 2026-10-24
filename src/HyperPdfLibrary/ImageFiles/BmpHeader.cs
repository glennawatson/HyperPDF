// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>What a bitmap's headers say about its pixels.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="TopDown">Whether the rows are stored from the top; otherwise they are stored from the bottom.</param>
/// <param name="BitsPerPixel">The bits in a pixel: 1, 2, 4, 8, 16, 24 or 32.</param>
/// <param name="Compression">The compression: 0 none, 1 RLE8, 2 RLE4, 3 or 6 colour masks.</param>
/// <param name="PaletteOffset">Where the palette starts in the file.</param>
/// <param name="PaletteEntries">How many colours the palette holds; zero when there is none.</param>
/// <param name="PaletteEntryBytes">The bytes in a palette entry: 3 for the oldest headers, otherwise 4.</param>
/// <param name="DataOffset">Where the pixel data starts in the file.</param>
/// <param name="Masks">The colour masks of 16 and 32-bit pixels.</param>
[DebuggerDisplay("BmpHeader: {Width}x{Height}, {BitsPerPixel} bpp, compression {Compression}")]
internal readonly record struct BmpHeader(
    int Width,
    int Height,
    bool TopDown,
    int BitsPerPixel,
    int Compression,
    int PaletteOffset,
    int PaletteEntries,
    int PaletteEntryBytes,
    int DataOffset,
    BmpMasks Masks);
