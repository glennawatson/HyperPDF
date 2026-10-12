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
}
