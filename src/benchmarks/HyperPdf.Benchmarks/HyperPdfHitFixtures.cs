// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>Creates ordered character arrays with focused hit-test queries.</summary>
internal static class HyperPdfHitFixtures
{
    /// <summary>The indexed dense page size.</summary>
    private const int DenseCharacters = 4096;

    /// <summary>A page below the indexing threshold.</summary>
    private const int SmallCharacters = 32;

    /// <summary>The first index in the second block.</summary>
    private const int SecondBlock = 32;

    /// <summary>The boxes on one row.</summary>
    private const int Columns = 64;

    /// <summary>The horizontal pitch.</summary>
    private const float PitchX = 10F;

    /// <summary>The vertical pitch.</summary>
    private const float PitchY = 14F;

    /// <summary>The glyph width.</summary>
    private const float Width = 8F;

    /// <summary>The glyph height.</summary>
    private const float Height = 10F;

    /// <summary>The tolerance width.</summary>
    private const float Tolerance = 4F;

    /// <summary>The angle used for rotated glyph geometry.</summary>
    private const float Rotation = 0.3F;

    /// <summary>The midpoint factor.</summary>
    private const float Half = 0.5F;

    /// <summary>The divisor selecting a middle character.</summary>
    private const int MiddleDivisor = 2;

    /// <summary>A coordinate outside either page.</summary>
    private const float Outside = -100F;

    /// <summary>The left edge of overlapping exact hits.</summary>
    private const float OverlapLeft = 2F;

    /// <summary>The right edge of overlapping exact hits.</summary>
    private const float OverlapRight = 4F;

    /// <summary>The point shared by overlapping exact hits.</summary>
    private const float OverlapPoint = 3F;

    /// <summary>Builds one focused geometry and query scenario.</summary>
    /// <param name="scenario">The page and query case.</param>
    /// <returns>The ordered records and query.</returns>
    internal static HyperPdfHitFixture Create(HyperPdfHitScenario scenario)
    {
        var characters = CreateGrid(CharacterCount(scenario));
        if (scenario == HyperPdfHitScenario.RotatedLate)
        {
            Rotate(characters);
        }

        if (scenario is HyperPdfHitScenario.OverlappingExact or HyperPdfHitScenario.OverlappingNearestTie)
        {
            return CreateOverlap(characters, scenario);
        }

        var box = characters[CharacterIndex(scenario, characters.Length)].Box;
        var point = new Vector2((box.Left + box.Right) * Half, (box.Bottom + box.Top) * Half);
        if (scenario is HyperPdfHitScenario.DenseMiss or HyperPdfHitScenario.SmallMiss)
        {
            point = new(Outside, Outside);
        }

        var tolerance = scenario is HyperPdfHitScenario.DenseTolerance or HyperPdfHitScenario.SmallTolerance ? Tolerance : 0;
        if (tolerance > 0)
        {
            point.X = box.Right + 1;
        }

        return new(characters, point, tolerance);
    }

    /// <summary>Builds separated glyph boxes in original row and column order.</summary>
    /// <param name="count">The character count.</param>
    /// <returns>The character records.</returns>
    internal static PdfTextChar[] CreateGrid(int count)
    {
        var characters = new PdfTextChar[count];
        for (var i = 0; i < characters.Length; i++)
        {
            var row = Math.DivRem(i, Columns, out var column);
            var x = column * PitchX;
            var y = row * PitchY;
            characters[i] = Character(new(x, y, x + Width, y + Height));
        }

        return characters;
    }

    /// <summary>Selects the small-page cases without changing the dense-page fixtures.</summary>
    /// <param name="scenario">The query case.</param>
    /// <returns>The character count.</returns>
    private static int CharacterCount(HyperPdfHitScenario scenario) => scenario switch
    {
        HyperPdfHitScenario.SmallEarly or HyperPdfHitScenario.SmallMiddle or HyperPdfHitScenario.SmallLate
            or HyperPdfHitScenario.SmallMiss or HyperPdfHitScenario.SmallTolerance => SmallCharacters,
        _ => DenseCharacters,
    };

    /// <summary>Selects the exact character used by position queries.</summary>
    /// <param name="scenario">The query case.</param>
    /// <param name="count">The character count.</param>
    /// <returns>The query character index.</returns>
    private static int CharacterIndex(HyperPdfHitScenario scenario, int count) => scenario switch
    {
        HyperPdfHitScenario.DenseEarly or HyperPdfHitScenario.SmallEarly => 0,
        HyperPdfHitScenario.DenseMiddle or HyperPdfHitScenario.SmallMiddle => count / MiddleDivisor,
        _ => count - 1,
    };

    /// <summary>Rotates each glyph box without changing character order.</summary>
    /// <param name="characters">The ordered character records.</param>
    private static void Rotate(PdfTextChar[] characters)
    {
        var matrix = Matrix3x2.CreateRotation(Rotation);
        for (var i = 0; i < characters.Length; i++)
        {
            var box = TextGeometry.TransformRect(matrix, characters[i].Box);
            characters[i] = Character(box);
        }
    }

    /// <summary>Places exact overlaps or equal nearest candidates in distinct blocks.</summary>
    /// <param name="characters">The ordered dense-page records.</param>
    /// <param name="scenario">The overlap case.</param>
    /// <returns>The geometry and query.</returns>
    private static HyperPdfHitFixture CreateOverlap(PdfTextChar[] characters, HyperPdfHitScenario scenario)
    {
        characters[0] = Character(new(0, 0, 1, 1));
        if (scenario == HyperPdfHitScenario.OverlappingNearestTie)
        {
            characters[SecondBlock] = characters[0];
            return new(characters, new(-1, 0), Tolerance);
        }

        characters[SecondBlock] = Character(new(OverlapLeft, 0, OverlapRight, OverlapLeft));
        characters[SecondBlock + 1] = characters[SecondBlock];
        return new(characters, new(OverlapPoint, 1), Tolerance);
    }

    /// <summary>Creates one glyph while preserving all fields of the scanned character record.</summary>
    /// <param name="box">The glyph geometry.</param>
    /// <returns>The original character record.</returns>
    private static PdfTextChar Character(PdfRectangle box) => new('A', PdfTextCharKind.Normal, 'A', new(box.Left, box.Bottom), box, box, Height, 0, false);
}
