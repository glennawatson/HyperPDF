// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Windows;

/// <summary>Native <c>BITMAPINFOHEADER</c> for 32 bit top-down BGRA pixels.</summary>
/// <param name="Size">The structure size.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height; negative for top-down rows.</param>
/// <param name="Planes">Always 1.</param>
/// <param name="BitCount">The bits per pixel.</param>
/// <param name="Compression">The compression, <c>BI_RGB</c>.</param>
/// <param name="SizeImage">The image size, or zero.</param>
/// <param name="XPelsPerMeter">The horizontal resolution, or zero.</param>
/// <param name="YPelsPerMeter">The vertical resolution, or zero.</param>
/// <param name="ColorsUsed">Colour table entries, zero.</param>
/// <param name="ColorsImportant">Important colours, zero.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct BitmapInfoHeader(
    int Size,
    int Width,
    int Height,
    short Planes,
    short BitCount,
    uint Compression,
    uint SizeImage,
    int XPelsPerMeter,
    int YPelsPerMeter,
    uint ColorsUsed,
    uint ColorsImportant)
{
    /// <summary>The size of the structure.</summary>
    internal const int StructureSize = 40;

    /// <summary>The bits in a BGRA pixel.</summary>
    private const short BitsPerPixel = 32;

    /// <summary>Describes top-down 32 bit pixels.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The header.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static BitmapInfoHeader TopDown(int width, int height) => new(StructureSize, width, -height, 1, BitsPerPixel, 0, 0, 0, 0, 0, 0);
}
