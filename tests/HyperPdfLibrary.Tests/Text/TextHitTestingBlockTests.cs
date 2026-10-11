// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks indexed hit testing against the original character-by-character search.</summary>
[NotInParallel]
public sealed class TextHitTestingBlockTests
{
    /// <summary>The number of characters in an indexed block.</summary>
    private const int BlockSize = 32;

    /// <summary>The first page size that receives an index.</summary>
    private const int IndexedSize = 128;

    /// <summary>A dense page size.</summary>
    private const int DenseSize = 1024;

    /// <summary>The number of boxes on one row.</summary>
    private const int CellsPerRow = 64;

    /// <summary>The horizontal pitch of grid boxes.</summary>
    private const float CellWidth = 10F;

    /// <summary>The vertical pitch of grid boxes.</summary>
    private const float CellHeight = 14F;

    /// <summary>The visible width of a grid box.</summary>
    private const float GlyphWidth = 8F;

    /// <summary>The visible height of a grid box.</summary>
    private const float GlyphHeight = 10F;

    /// <summary>The reach of tolerant queries.</summary>
    private const float Tolerance = 4F;

    /// <summary>The original initial distance on each axis.</summary>
    private const double FarAway = 5000;

    /// <summary>Half, for the midpoint and splitting a tolerance.</summary>
    private const float Half = 0.5F;

    /// <summary>The divisor used to select a middle index.</summary>
    private const int MiddleDivisor = 2;

    /// <summary>The offset of the NaN box after the indexed threshold.</summary>
    private const int NanOffset = 3;

    /// <summary>The offset of the infinite box after the indexed threshold.</summary>
    private const int InfiniteOffset = 4;

    /// <summary>The horizontal coordinate of a neighboring cell.</summary>
    private const float NeighborX = 20F;

    /// <summary>A point within the first grid box.</summary>
    private const float Inside = 5F;

    /// <summary>A distant point beyond the page grid.</summary>
    private const float FarX = 640F;

    /// <summary>A distant vertical coordinate.</summary>
    private const float FarY = 1000F;

    /// <summary>A distant negative coordinate.</summary>
    private const float NegativeFar = -100F;

    /// <summary>The x coordinate of the exact hit in the ordering case.</summary>
    private const float ExactX = 3F;

    /// <summary>The left edge of the exact overlap boxes.</summary>
    private const float OverlapLeft = 2F;

    /// <summary>The right edge of the exact overlap boxes.</summary>
    private const float OverlapRight = 4F;

    /// <summary>The lower coordinate of a reversed box.</summary>
    private const float ReversedLow = 20F;

    /// <summary>The higher coordinate of a reversed box.</summary>
    private const float ReversedHigh = 30F;

    /// <summary>The coordinate of a zero-area box.</summary>
    private const float PointBox = 50F;

    /// <summary>The lower coordinate of a rotated box.</summary>
    private const float RotatedLow = 60F;

    /// <summary>The higher coordinate of a rotated box.</summary>
    private const float RotatedHigh = 70F;

    /// <summary>The angle of a rotated text box.</summary>
    private const float Rotation = 0.3F;

    /// <summary>The character code of the test glyph.</summary>
    private const int CharacterCode = (int)'A';

    /// <summary>The nominal font size of the test glyph.</summary>
    private const float FontSize = 10F;

    /// <summary>One line of visible text in the extraction parity page.</summary>
    private const string ExtractionLine = "The quick brown fox jumps over the lazy dog while reading accessible text";

    /// <summary>Small and dense pages preserve exact, miss, nearest, and tie behavior.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GridQueriesMatchScalarSearch()
    {
        foreach (var count in new[] { 0, 1, BlockSize - 1, IndexedSize - 1, IndexedSize, IndexedSize + 1, DenseSize })
        {
            var chars = CreateGrid(count);
            var page = CreatePage(chars);
            await Assert.That(TextHitTesting.BuildBlocks(chars).Length).IsEqualTo(count < IndexedSize ? 0 : 1 + ((count - 1) / BlockSize));

            var queries = new[]
            {
                new HitQuery(new(1, 1), 0, 0),
                new HitQuery(new(1, 1), Tolerance, Tolerance),
                new HitQuery(new(CellWidth, 1), Tolerance, Tolerance),
                new HitQuery(new(FarX, FarY), Tolerance, Tolerance),
                new HitQuery(new(NegativeFar, NegativeFar), Tolerance, Tolerance),
                new HitQuery(new(NeighborX, CellHeight), 0, 0),
                new HitQuery(new(NeighborX, CellHeight), Tolerance, Tolerance),
                new HitQuery(new(Inside, Inside), -Tolerance, Tolerance),
                new HitQuery(new(Inside, Inside), Tolerance, -Tolerance),
            };

            foreach (var query in queries)
            {
                await Check(page, chars, query);
            }

            foreach (var index in new[] { count / MiddleDivisor, Math.Max(0, count - 1) })
            {
                var box = count == 0 ? default : chars[index].Box;
                await Check(page, chars, new(new(box.Left + 1, box.Bottom + 1), 0, 0));
                await Check(page, chars, new(new(box.Right + 1, box.Bottom + 1), Tolerance, Tolerance));
            }
        }
    }

    /// <summary>Earlier tolerance candidates never hide later exact hits, while exact overlaps and nearest ties keep the first index.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrderingAndPathologicalBoxesMatchScalarSearch()
    {
        var chars = CreateGrid(DenseSize);
        chars[0] = Character(new(0, 0, 1, 1));
        chars[BlockSize] = Character(new(OverlapLeft, 0, OverlapRight, OverlapLeft));
        chars[BlockSize + 1] = Character(new(OverlapLeft, 0, OverlapRight, OverlapLeft));
        chars[IndexedSize] = Character(new(ReversedHigh, ReversedHigh, ReversedLow, ReversedLow));
        chars[IndexedSize + 1] = Character(new(PointBox, PointBox, PointBox, PointBox));
        chars[IndexedSize + MiddleDivisor] = Character(TextGeometry.TransformRect(Matrix3x2.CreateRotation(Rotation), new(RotatedLow, RotatedLow, RotatedHigh, RotatedHigh)));
        chars[IndexedSize + NanOffset] = Character(new(float.NaN, 0, 1, 1));
        chars[IndexedSize + InfiniteOffset] = Character(new(float.NegativeInfinity, 0, 1, 1));
        var page = CreatePage(chars);

        var queries = new[]
        {
            new HitQuery(new(ExactX, 1), Tolerance, Tolerance),
            new HitQuery(new(OverlapLeft, 1), 0, 0),
            new HitQuery(new(ReversedLow + Inside, ReversedLow + Inside), 0, 0),
            new HitQuery(new(PointBox, PointBox), 0, 0),
            new HitQuery(new(RotatedLow + Inside, RotatedLow + Inside), Tolerance, Tolerance),
            new HitQuery(new(-1, 0), Tolerance, 0),
            new HitQuery(new(float.PositiveInfinity, 0), Tolerance, Tolerance),
            new HitQuery(new(float.NaN, 0), Tolerance, Tolerance),
            new HitQuery(new(ExactX, 1), float.PositiveInfinity, 0),
            new HitQuery(new(ExactX, 1), -Tolerance, Tolerance),
        };

        foreach (var query in queries)
        {
            await Check(page, chars, query);
        }

        await Assert.That(page.GetIndexAtPosition(new(ExactX, 1), Tolerance, Tolerance)).IsEqualTo(BlockSize);
        await Assert.That(page.GetIndexAtPosition(new(OverlapLeft, 1), 0, 0)).IsEqualTo(BlockSize);
    }

    /// <summary>Real extracted glyphs and generated spacing match a scalar scan across indexed blocks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractedPageMatchesScalarSearch()
    {
        const string content = $"BT /F1 10 Tf 14 TL 10 200 Td ({ExtractionLine}) Tj T* ({ExtractionLine}) Tj T* ({ExtractionLine}) Tj ET";
        var page = TextTestDocument.Extract(content);
        var chars = page.Chars.ToArray();
        await Assert.That(chars.Length).IsGreaterThanOrEqualTo(IndexedSize);

        for (var i = 0; i < chars.Length; i += BlockSize)
        {
            var box = chars[i].Box;
            var center = new Vector2((box.Left + box.Right) * Half, (box.Bottom + box.Top) * Half);
            await Check(page, chars, new(center, 0, 0));
            await Check(page, chars, new(center, Tolerance, Tolerance));
        }

        await Check(page, chars, new(new(FarX, FarY), Tolerance, Tolerance));
    }

    /// <summary>Indexed queries do not allocate after a page is constructed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WarmQueriesDoNotAllocate()
    {
        var chars = CreateGrid(DenseSize);
        var page = CreatePage(chars);
        var middle = chars[DenseSize / MiddleDivisor].Box;
        var last = chars[^1].Box;
        Vector2[] points =
        [
            new(Inside, Inside),
            new(middle.Left + 1, middle.Bottom + 1),
            new(last.Left + 1, last.Bottom + 1),
            new(last.Right + 1, last.Bottom + 1),
            new(NegativeFar, NegativeFar),
        ];
        foreach (var point in points)
        {
            _ = page.GetIndexAtPosition(point, Tolerance, Tolerance);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < IndexedSize; i++)
        {
            foreach (var point in points)
            {
                _ = page.GetIndexAtPosition(point, Tolerance, Tolerance);
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>Wraps geometry in a text page without creating a document for every character.</summary>
    /// <param name="chars">The page characters.</param>
    /// <returns>The text page.</returns>
    private static PdfTextPage CreatePage(PdfTextChar[] chars)
    {
        var sample = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (A) Tj ET");
        return new(sample.Page, chars, new int[chars.Length], string.Empty, []);
    }

    /// <summary>Creates ordered, separate glyph boxes.</summary>
    /// <param name="count">The number of characters.</param>
    /// <returns>The characters.</returns>
    private static PdfTextChar[] CreateGrid(int count)
    {
        var chars = new PdfTextChar[count];
        for (var i = 0; i < chars.Length; i++)
        {
            var row = Math.DivRem(i, CellsPerRow, out var column);
            var x = column * CellWidth;
            var y = row * CellHeight;
            chars[i] = Character(new(x, y, x + GlyphWidth, y + GlyphHeight));
        }

        return chars;
    }

    /// <summary>Creates one normal glyph with the supplied outline box.</summary>
    /// <param name="box">The outline box.</param>
    /// <returns>The character.</returns>
    private static PdfTextChar Character(PdfRectangle box) =>
        new('A', PdfTextCharKind.Normal, CharacterCode, new(box.Left, box.Bottom), box, box, FontSize, 0, false);

    /// <summary>Checks an indexed query against the scalar reference.</summary>
    /// <param name="page">The indexed text page.</param>
    /// <param name="chars">The original characters.</param>
    /// <param name="query">The query to check.</param>
    /// <returns>A task.</returns>
    private static async Task Check(PdfTextPage page, PdfTextChar[] chars, HitQuery query)
    {
        var expected = Scalar(chars, query.Point, query.ToleranceX, query.ToleranceY);
        await Assert.That(page.GetIndexAtPosition(query.Point, query.ToleranceX, query.ToleranceY)).IsEqualTo(expected);
    }

    /// <summary>Runs the pre-index scalar algorithm as the independent reference.</summary>
    /// <param name="chars">The original characters.</param>
    /// <param name="point">The query point.</param>
    /// <param name="toleranceX">The horizontal tolerance.</param>
    /// <param name="toleranceY">The vertical tolerance.</param>
    /// <returns>The matching index, or -1.</returns>
    private static int Scalar(ReadOnlySpan<PdfTextChar> chars, Vector2 point, float toleranceX, float toleranceY)
    {
        var nearest = -1;
        var bestX = FarAway;
        var bestY = FarAway;
        var useTolerance = toleranceX > 0 || toleranceY > 0;
        for (var i = 0; i < chars.Length; i++)
        {
            var box = chars[i].Box;
            if (TextGeometry.Contains(box, point))
            {
                return i;
            }

            box = TextGeometry.Normalize(box);
            var halfX = toleranceX * Half;
            var halfY = toleranceY * Half;
            var expanded = new PdfRectangle(box.Left - halfX, box.Bottom - halfY, box.Right + halfX, box.Top + halfY);
            if (!useTolerance || !TextGeometry.Contains(expanded, point))
            {
                continue;
            }

            double dx = MathF.Min(MathF.Abs(point.X - box.Left), MathF.Abs(point.X - box.Right));
            double dy = MathF.Min(MathF.Abs(point.Y - box.Bottom), MathF.Abs(point.Y - box.Top));
            if (dx + dy >= bestX + bestY)
            {
                continue;
            }

            bestX = dx;
            bestY = dy;
            nearest = i;
        }

        return nearest;
    }

    /// <summary>A point and its two tolerances for parity checks.</summary>
    /// <param name="Point">The query point.</param>
    /// <param name="ToleranceX">The horizontal tolerance.</param>
    /// <param name="ToleranceY">The vertical tolerance.</param>
    private readonly record struct HitQuery(Vector2 Point, float ToleranceX, float ToleranceY);
}
