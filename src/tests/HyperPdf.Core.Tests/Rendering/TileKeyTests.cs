// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Core.Tests.Rendering;

/// <summary>Tests for <see cref="TileKey"/> equality and hashing.</summary>
public sealed class TileKeyTests
{
    /// <summary>A document identifier used by the baseline key.</summary>
    private const int Document = 3;

    /// <summary>A page index used by the baseline key.</summary>
    private const int Page = 4;

    /// <summary>A scale key used by the baseline key.</summary>
    private const int Scale = 5;

    /// <summary>A tone identifier used by the baseline key.</summary>
    private const int Tone = 6;

    /// <summary>A column used by the baseline key.</summary>
    private const short TileColumn = 7;

    /// <summary>A row used by the baseline key.</summary>
    private const short TileRow = 8;

    /// <summary>A value that differs from every baseline field.</summary>
    private const int Different = 9;

    /// <summary>A realistic scale key.</summary>
    private const int RealisticScale = 2048;

    /// <summary>The number of pages in the hash grid.</summary>
    private const int GridPages = 40;

    /// <summary>The number of columns in the hash grid.</summary>
    private const int GridColumns = 8;

    /// <summary>The number of rows in the hash grid.</summary>
    private const int GridRows = 8;

    /// <summary>A key whose fields are all different from zero.</summary>
    private static readonly TileKey Baseline = new(Document, Page, Scale, PageRotation.Rotate90, Tone, TileColumn, TileRow);

    /// <summary>Verifies keys with equal fields are equal and hash the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EqualKeysHashTheSame()
    {
        var copy = new TileKey(Document, Page, Scale, PageRotation.Rotate90, Tone, TileColumn, TileRow);

        await Assert.That(copy.Equals(Baseline)).IsTrue();
        await Assert.That(copy == Baseline).IsTrue();
        await Assert.That(copy.GetHashCode()).IsEqualTo(Baseline.GetHashCode());
    }

    /// <summary>Verifies changing any one field makes the keys unequal.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EachFieldAffectsEquality()
    {
        TileKey[] others =
        [
            Baseline with { DocumentId = Different },
            Baseline with { PageIndex = Different },
            Baseline with { ScaleKey = Different },
            Baseline with { Rotation = PageRotation.Rotate180 },
            Baseline with { ToneId = Different },
            Baseline with { Column = Different },
            Baseline with { Row = Different },
        ];

        foreach (var other in others)
        {
            await Assert.That(other.Equals(Baseline)).IsFalse();
            await Assert.That(other != Baseline).IsTrue();
        }
    }

    /// <summary>Verifies a grid of distinct tile keys produces distinct hashes, so the packing does not collide.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DistinctTileGridKeysHaveDistinctHashes()
    {
        var hashes = new HashSet<int>();
        for (var page = 0; page < GridPages; page++)
        {
            for (var column = 0; column < GridColumns; column++)
            {
                for (var row = 0; row < GridRows; row++)
                {
                    _ = hashes.Add(new TileKey(1, page, RealisticScale, PageRotation.None, 0, (short)column, (short)row).GetHashCode());
                }
            }
        }

        await Assert.That(hashes.Count).IsEqualTo(GridPages * GridColumns * GridRows);
    }

    /// <summary>Verifies negative document identifiers and preview coordinates are distinguished from their positive forms.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NegativeValuesAreDistinct()
    {
        var preview = TileKey.Preview(1, Page, PageRotation.None, 0);
        var negativeDocument = TileKey.Preview(~1, Page, PageRotation.None, 0);

        await Assert.That(preview.IsPreview).IsTrue();
        await Assert.That(preview.Equals(negativeDocument)).IsFalse();
        await Assert.That(preview.GetHashCode()).IsNotEqualTo(negativeDocument.GetHashCode());
    }
}
