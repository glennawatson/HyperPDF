// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Media.Native.VaApi;

/// <summary>A <c>VAPictureH264</c>: a surface and how the H.264 decoding process sees the picture in it.</summary>
/// <param name="Surface">The surface the picture is in.</param>
/// <param name="FrameIndex">The frame number, or the long-term frame index of a long-term picture.</param>
/// <param name="Flags">The flags: <see cref="Invalid"/>, <see cref="ShortTerm"/> or <see cref="LongTerm"/>.</param>
/// <param name="TopOrderCount">The picture order count of the top field.</param>
/// <param name="BottomOrderCount">The picture order count of the bottom field.</param>
[DebuggerDisplay("PictureEntry: surface {Surface}, frame {FrameIndex}")]
internal readonly record struct PictureEntry(uint Surface, uint FrameIndex, uint Flags, int TopOrderCount, int BottomOrderCount)
{
    /// <summary>The flag for an entry that holds no picture.</summary>
    internal const uint Invalid = 0x1;

    /// <summary>The flag for a picture kept as a short-term reference.</summary>
    internal const uint ShortTerm = 0x8;

    /// <summary>The flag for a picture kept as a long-term reference.</summary>
    internal const uint LongTerm = 0x10;

    /// <summary>The size of a <c>VAPictureH264</c> in bytes.</summary>
    internal const int Size = 36;

    /// <summary>The surface identity that means no surface.</summary>
    internal const uint NoSurface = uint.MaxValue;

    /// <summary>Gets an entry that holds no picture.</summary>
    internal static PictureEntry None => new(NoSurface, 0, Invalid, 0, 0);
}
