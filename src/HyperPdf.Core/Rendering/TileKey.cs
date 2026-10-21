// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Rendering;

/// <summary>Identifies one rendered tile. Preview images use <see cref="Column"/> and <see cref="Row"/> of -1.</summary>
/// <param name="DocumentId">The document identifier.</param>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="ScaleKey">The quantised render scale, see <see cref="TileGrid.ToScaleKey"/>.</param>
/// <param name="Rotation">The page rotation.</param>
/// <param name="ToneId">The <see cref="PageTone.Id"/> applied to the pixels.</param>
/// <param name="Column">The tile column.</param>
/// <param name="Row">The tile row.</param>
[DebuggerDisplay("TileKey: Doc {DocumentId} p{PageIndex} s{ScaleKey} ({Column},{Row})")]
public readonly record struct TileKey(int DocumentId, int PageIndex, int ScaleKey, PageRotation Rotation, int ToneId, short Column, short Row)
{
    /// <summary>The bits in a half of a 64 bit word.</summary>
    private const int HalfWordBits = 32;

    /// <summary>The bits in a tile coordinate.</summary>
    private const int CoordinateBits = 16;

    /// <summary>The first hash multiplier, an odd 64 bit constant with well mixed bits.</summary>
    private const ulong MultiplierOne = 0x9E3779B97F4A7C15UL;

    /// <summary>The second hash multiplier.</summary>
    private const ulong MultiplierTwo = 0xC2B2AE3D27D4EB4FUL;

    /// <summary>The third hash multiplier.</summary>
    private const ulong MultiplierThree = 0x165667B19E3779F9UL;

    /// <summary>Gets a value indicating whether the key identifies a whole-page preview image.</summary>
    public bool IsPreview => Column < 0;

    /// <summary>Creates the key of a page preview image.</summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="rotation">The rotation.</param>
    /// <param name="toneId">The page tone identifier.</param>
    /// <returns>The key.</returns>
    public static TileKey Preview(int documentId, int pageIndex, PageRotation rotation, int toneId) =>
        new(documentId, pageIndex, 0, rotation, toneId, -1, -1);

    /// <summary>Determines whether another key identifies the same tile.</summary>
    /// <param name="other">The other key.</param>
    /// <returns><see langword="true"/> when every field is equal.</returns>
    public bool Equals(TileKey other) =>
        DocumentId == other.DocumentId
        && PageIndex == other.PageIndex
        && ScaleKey == other.ScaleKey
        && Column == other.Column
        && Row == other.Row
        && ToneId == other.ToneId
        && Rotation == other.Rotation;

    /// <summary>Gets a hash that packs the seven fields into three words and mixes them.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var first = (uint)DocumentId | ((ulong)(uint)PageIndex << HalfWordBits);
        var second = (uint)ScaleKey | ((ulong)(uint)ToneId << HalfWordBits);
        var third = (ushort)Column | ((ulong)(ushort)Row << CoordinateBits) | ((ulong)(uint)Rotation << HalfWordBits);
        var mixed = first * MultiplierOne;
        mixed = (mixed ^ second) * MultiplierTwo;
        mixed = (mixed ^ third) * MultiplierThree;
        return (int)(mixed >> HalfWordBits);
    }
}
