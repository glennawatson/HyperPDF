// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The twelve adaptive template pixels A1 to A12 of the extended generic template (T.88 figure 3(b), EXTTEMPLATE = 1), as offsets
/// from the pixel being decoded. The pixels are held four at a time; a template with fewer adaptive pixels uses the first group.
/// </summary>
/// <param name="First">The pixels A1 to A4.</param>
/// <param name="Second">The pixels A5 to A8.</param>
/// <param name="Third">The pixels A9 to A12.</param>
internal readonly record struct Jbig2ExtendedAtPixels(Jbig2AtPixels First, Jbig2AtPixels Second, Jbig2AtPixels Third)
{
    /// <summary>The adaptive pixels in each group.</summary>
    internal const int GroupSize = 4;

    /// <summary>The adaptive pixels of the extended template.</summary>
    internal const int Count = 12;

    /// <summary>The offset two columns to the left.</summary>
    private const int LeftTwo = -2;

    /// <summary>The offset two rows up.</summary>
    private const int UpTwo = -2;

    /// <summary>The column offset three to the left.</summary>
    private const int LeftThree = -3;

    /// <summary>The column offset four to the left.</summary>
    private const int LeftFour = -4;

    /// <summary>The column offset two to the right.</summary>
    private const int RightTwo = 2;

    /// <summary>The column offset three to the right.</summary>
    private const int RightThree = 3;

    /// <summary>Gets the pixels at their nominal locations (T.88 table 5, GBTEMPLATE 0 with EXTTEMPLATE 1).</summary>
    internal static Jbig2ExtendedAtPixels Nominal { get; } = new(
        new(LeftTwo, 0, 0, UpTwo, LeftTwo, -1, -1, UpTwo),
        new(1, UpTwo, RightTwo, -1, LeftThree, 0, LeftFour, 0),
        new(RightTwo, UpTwo, RightThree, -1, LeftTwo, UpTwo, LeftThree, -1));

    /// <summary>Gets the pixels A1 to A4 of a template with four or fewer adaptive pixels.</summary>
    /// <param name="first">The first group.</param>
    /// <returns>The pixels.</returns>
    internal static Jbig2ExtendedAtPixels FromFirst(in Jbig2AtPixels first) => new(first, default, default);

    /// <summary>Gets the column offset of a pixel.</summary>
    /// <param name="index">The pixel, 0 for A1 to 11 for A12.</param>
    /// <returns>The offset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int X(int index) => Group(index).X(index % GroupSize);

    /// <summary>Gets the row offset of a pixel.</summary>
    /// <param name="index">The pixel, 0 for A1 to 11 for A12.</param>
    /// <returns>The offset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Y(int index) => Group(index).Y(index % GroupSize);

    /// <summary>Gets the group of four pixels that holds a pixel.</summary>
    /// <param name="index">The pixel, 0 for A1 to 11 for A12.</param>
    /// <returns>The group.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Jbig2AtPixels Group(int index) => (index / GroupSize) switch
    {
        0 => First,
        1 => Second,
        _ => Third,
    };
}
