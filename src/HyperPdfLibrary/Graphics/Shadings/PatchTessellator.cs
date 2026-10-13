// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Graphics.Shadings;

/// <summary>Turns a tensor-product patch into a grid of triangles with bilinearly blended corner colours.</summary>
internal static class PatchTessellator
{
    /// <summary>The most cells along each side of a patch.</summary>
    private const int MaxCells = 32;

    /// <summary>The most vertices along each side of a patch.</summary>
    private const int MaxSide = MaxCells + 1;

    /// <summary>The fewest cells along each side, so colours blend bilinearly rather than across one diagonal.</summary>
    private const int MinCells = 2;

    /// <summary>The longest cell edge, in device units of the space the mesh is drawn in.</summary>
    private const float TargetCellSize = 3;

    /// <summary>The corner colour difference, in channel steps, one cell may span.</summary>
    private const float ColorStepPerCell = 32;

    /// <summary>The control points along each side of a patch.</summary>
    private const int Control = 4;

    /// <summary>The interior control points of a patch.</summary>
    private const int InteriorPoints = 4;

    /// <summary>The control points one interior point is computed from.</summary>
    private const int TermsPerPoint = 8;

    /// <summary>The weight of the nearest corner.</summary>
    private const float CornerWeight = 4;

    /// <summary>The weight of the boundary points next to the corner.</summary>
    private const float EdgeWeight = 6;

    /// <summary>The weight of the corners at the ends of the sides.</summary>
    private const float FarWeight = 2;

    /// <summary>The weight of the points beyond the sides.</summary>
    private const float CrossWeight = 3;

    /// <summary>The divisor of the interior point formula.</summary>
    private const float Divisor = 9;

    /// <summary>The coefficient of the middle Bernstein weights.</summary>
    private const float BernsteinTriple = 3;

    /// <summary>Gets the index of each interior control point: p11, p12, p21, p22.</summary>
    private static ReadOnlySpan<byte> InteriorTargets => [0x05, 0x06, 0x09, 0x0A];

    /// <summary>Gets the indices, per interior point, of the corner, its two neighbours, the two far corners, the two cross points and the opposite corner.</summary>
    private static ReadOnlySpan<byte> InteriorTerms =>
    [
        0x00, 0x01, 0x04, 0x03, 0x0C, 0x0D, 0x07, 0x0F,
        0x03, 0x02, 0x07, 0x00, 0x0F, 0x0E, 0x04, 0x0C,
        0x0C, 0x0D, 0x08, 0x0F, 0x00, 0x01, 0x0B, 0x03,
        0x0F, 0x0E, 0x0B, 0x0C, 0x03, 0x02, 0x08, 0x00,
    ];

    /// <summary>
    /// Adds the triangles of one patch. The grid is adaptive, so small patches cost few triangles: each
    /// direction gets enough cells that no cell edge spans more than <see cref="TargetCellSize"/> device units and no
    /// cell spans a large colour change, bounded between <see cref="MinCells"/> and <see cref="MaxCells"/>.
    /// </summary>
    /// <param name="builder">The mesh builder.</param>
    /// <param name="points">The 16 control points, p[i * 4 + j].</param>
    /// <param name="corners">The corner colours: c00, c03, c33, c30.</param>
    /// <param name="scale">The device units one unit of shading space spans.</param>
    internal static void Emit(MeshBuilder builder, ReadOnlySpan<PdfPoint> points, ReadOnlySpan<MeshVertex> corners, float scale)
    {
        var colorCells = ColorSteps(corners) / ColorStepPerCell;
        var cellsU = CellCount(PolygonLength(points, Control, 1) * scale, colorCells);
        var cellsV = CellCount(PolygonLength(points, 1, Control) * scale, colorCells);
        var side = cellsV + 1;
        Span<MeshVertex> grid = stackalloc MeshVertex[MaxSide * MaxSide];
        for (var iu = 0; iu <= cellsU; iu++)
        {
            var u = (float)iu / cellsU;
            for (var iv = 0; iv <= cellsV; iv++)
            {
                var v = (float)iv / cellsV;
                var left = MeshVertex.Mix(corners[0], corners[1], v);
                var right = MeshVertex.Mix(corners[3], corners[2], v);
                var blended = MeshVertex.Mix(left, right, u);
                grid[(iu * side) + iv] = blended with { Point = Evaluate(points, u, v) };
            }
        }

        for (var iu = 0; iu < cellsU; iu++)
        {
            for (var iv = 0; iv < cellsV; iv++)
            {
                var a = grid[(iu * side) + iv];
                var b = grid[(iu * side) + iv + 1];
                var c = grid[((iu + 1) * side) + iv];
                var d = grid[((iu + 1) * side) + iv + 1];
                builder.AddTriangle(a, b, c);
                builder.AddTriangle(b, d, c);
            }
        }
    }

    /// <summary>Fills the four interior control points of a Coons patch from its boundary (PDF 32000 §8.7.4.5.8).</summary>
    /// <param name="p">The control points; the boundary is set, the interior is written.</param>
    internal static void FillCoonsInterior(Span<PdfPoint> p)
    {
        for (var row = 0; row < InteriorPoints; row++)
        {
            p[InteriorTargets[row]] = Interior(p, row);
        }
    }

    /// <summary>Chooses the cells along one direction.</summary>
    /// <param name="deviceLength">The longest control polygon along the direction, in device units.</param>
    /// <param name="colorCells">The cells the colour change asks for.</param>
    /// <returns>The cell count.</returns>
    private static int CellCount(float deviceLength, float colorCells)
    {
        var wanted = Math.Max(deviceLength / TargetCellSize, colorCells);
        return float.IsFinite(wanted) ? Math.Clamp((int)MathF.Ceiling(wanted), MinCells, MaxCells) : MaxCells;
    }

    /// <summary>Gets the longest of the four control polygons that run along one direction of a patch.</summary>
    /// <param name="p">The 16 control points.</param>
    /// <param name="step">The index step between points along the direction.</param>
    /// <param name="across">The index step between the polygons.</param>
    /// <returns>The length in shading space.</returns>
    private static float PolygonLength(ReadOnlySpan<PdfPoint> p, int step, int across)
    {
        var longest = 0F;
        for (var k = 0; k < Control; k++)
        {
            var length = 0F;
            for (var i = 0; i < Control - 1; i++)
            {
                var from = p[(k * across) + (i * step)];
                var to = p[(k * across) + ((i + 1) * step)];
                length += PdfPoint.Distance(from, to);
            }

            longest = Math.Max(longest, length);
        }

        return longest;
    }

    /// <summary>Gets the largest channel difference between any two corners of a patch, or the input range for a ramp.</summary>
    /// <param name="corners">The corner colours.</param>
    /// <returns>The difference in channel steps, from 0 to 255.</returns>
    private static float ColorSteps(ReadOnlySpan<MeshVertex> corners)
    {
        var largest = 0F;
        for (var i = 0; i < corners.Length; i++)
        {
            for (var j = i + 1; j < corners.Length; j++)
            {
                var a = corners[i].Color;
                var b = corners[j].Color;
                largest = Math.Max(largest, Math.Max(Math.Abs(a.Red - b.Red), Math.Max(Math.Abs(a.Green - b.Green), Math.Abs(a.Blue - b.Blue))));
            }
        }

        return largest;
    }

    /// <summary>Evaluates a surface point.</summary>
    /// <param name="p">The 16 control points.</param>
    /// <param name="u">The parameter along i.</param>
    /// <param name="v">The parameter along j.</param>
    /// <returns>The point.</returns>
    private static PdfPoint Evaluate(ReadOnlySpan<PdfPoint> p, float u, float v)
    {
        Span<float> bu = stackalloc float[Control];
        Span<float> bv = stackalloc float[Control];
        Bernstein(u, bu);
        Bernstein(v, bv);
        float x = 0;
        float y = 0;
        for (var i = 0; i < Control; i++)
        {
            for (var j = 0; j < Control; j++)
            {
                var weight = bu[i] * bv[j];
                x += p[(i * Control) + j].X * weight;
                y += p[(i * Control) + j].Y * weight;
            }
        }

        return new(x, y);
    }

    /// <summary>Computes the cubic Bernstein weights.</summary>
    /// <param name="t">The parameter.</param>
    /// <param name="weights">Receives four weights.</param>
    private static void Bernstein(float t, Span<float> weights)
    {
        var s = 1 - t;
        weights[0] = s * s * s;
        weights[1] = BernsteinTriple * t * s * s;
        weights[2] = BernsteinTriple * t * t * s;
        weights[3] = t * t * t;
    }

    /// <summary>Computes one interior point from the boundary points around it.</summary>
    /// <param name="p">The control points.</param>
    /// <param name="row">The row of <see cref="InteriorTerms"/> that names the points used.</param>
    /// <returns>The interior point.</returns>
    private static PdfPoint Interior(ReadOnlySpan<PdfPoint> p, int row)
    {
        var terms = InteriorTerms.Slice(row * TermsPerPoint, TermsPerPoint);
        var corner = p[terms[0]];
        var edgeA = p[terms[1]];
        var edgeB = p[terms[2]];
        var farA = p[terms[3]];
        var farB = p[terms[4]];
        var crossA = p[terms[5]];
        var crossB = p[terms[6]];
        var opposite = p[terms[7]];
        var x = (-CornerWeight * corner.X) + (EdgeWeight * (edgeA.X + edgeB.X)) - (FarWeight * (farA.X + farB.X)) + (CrossWeight * (crossA.X + crossB.X)) - opposite.X;
        var y = (-CornerWeight * corner.Y) + (EdgeWeight * (edgeA.Y + edgeB.Y)) - (FarWeight * (farA.Y + farB.Y)) + (CrossWeight * (crossA.Y + crossB.Y)) - opposite.Y;
        return new(x / Divisor, y / Divisor);
    }
}
