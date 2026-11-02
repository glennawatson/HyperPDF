// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// Decodes the free-form curve and surface extension (ECMA-363 clause 11): the object declaration and the surfaces of its
/// continuation blocks. Surfaces are tessellated into meshes. Faces, trim curves and edges are counted and not applied.
/// </summary>
internal static class U3dFreeFormDecoder
{
    /// <summary>The surface attribute flag for a non-rational surface, whose control points carry no weight.</summary>
    private const uint NonRational = 1;

    /// <summary>The surface attribute flag for a Bezier surface.</summary>
    private const uint Bezier = 2;

    /// <summary>The surface attribute flag for uniform knots in the U direction.</summary>
    private const uint UniformU = 4;

    /// <summary>The surface attribute flag for uniform knots in the V direction.</summary>
    private const uint UniformV = 8;

    /// <summary>The surface attribute flag for independent knots, which are not decoded.</summary>
    private const uint IndependentKnots = 0x10;

    /// <summary>The bytes of a control point's position: three 32-bit floats.</summary>
    private const int PositionBytes = 12;

    /// <summary>The bytes of a 32-bit value.</summary>
    private const int WordBytes = 4;

    /// <summary>The fields every surface starts with: attributes, shading index, two degrees and two counts.</summary>
    private const int SurfaceFields = 6;

    /// <summary>The fewest bytes a surface takes.</summary>
    private const int SurfaceMinimum = SurfaceFields * WordBytes;

    /// <summary>The values in a colour: red, green, blue and alpha.</summary>
    private const int ColorValues = 4;

    /// <summary>The most dimensions a texture coordinate has.</summary>
    private const uint MaxDimensions = 4;

    /// <summary>Reads a free-form object declaration.</summary>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the declaration.</param>
    internal static void ReadDeclaration(ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        var reader = builder.Open(data, null);
        var name = reader.ReadString();
        _ = reader.ReadU32();
        var attributes = reader.ReadU32();
        var surfaces = reader.ReadU32();
        var faces = reader.ReadU32();
        var edges = reader.ReadU32();
        var shadings = U3dShadingList.ReadAll(ref reader);
        if (reader.Failed || shadings is null)
        {
            builder.Problem("A free-form object declaration is cut short.");
            return;
        }

        if (surfaces > builder.Limits.MaxNodes)
        {
            builder.Problem($"The free-form object {name} has more surfaces than will be decoded.");
            return;
        }

        builder.FreeForms[name] = new(name, attributes, surfaces, faces, edges, shadings);
    }

    /// <summary>Reads the surfaces of a free-form object continuation and adds them to the scene as meshes.</summary>
    /// <param name="data">The block's data section.</param>
    /// <param name="builder">Receives the meshes.</param>
    internal static void ReadContinuation(ReadOnlyMemory<byte> data, U3dModelBuilder builder)
    {
        var name = builder.Open(data, null).ReadString();
        if (!builder.FreeForms.TryGetValue(name, out var declaration))
        {
            builder.Problem("A free-form continuation names an object the file does not declare.");
            return;
        }

        var reader = builder.Open(data, name);
        _ = reader.ReadString();
        _ = reader.ReadU32();
        var start = reader.ReadU32();
        var end = reader.ReadU32();
        if (reader.Failed || end < start || end > declaration.Surfaces || (long)(end - start) * SurfaceMinimum > reader.RemainingBytes)
        {
            builder.Problem($"A continuation of the free-form object {name} names surfaces the declaration does not have.");
            return;
        }

        for (var i = start; i < end; i++)
        {
            builder.Token.ThrowIfCancellationRequested();
            if (!ReadSurface(ref reader, declaration, builder))
            {
                return;
            }
        }

        if (declaration.Faces != 0)
        {
            builder.Problem($"The free-form object {name} has trim curves, which are not applied; each surface is drawn whole.");
        }
    }

    /// <summary>Reads one surface and adds its mesh.</summary>
    /// <param name="reader">The block's reader, placed at the surface.</param>
    /// <param name="declaration">The object declaration.</param>
    /// <param name="builder">Receives the mesh and problems.</param>
    /// <returns><see langword="false"/> when the block cannot be read any further.</returns>
    private static bool ReadSurface(ref U3dBitReader reader, U3dFreeFormDeclaration declaration, U3dModelBuilder builder)
    {
        var attributes = reader.ReadU32();
        var shading = reader.ReadCompressedU32(U3dContexts.Shading);
        var degreeU = reader.ReadU32();
        var degreeV = reader.ReadU32();
        var knotsU = ReadKnots(ref reader, declaration, attributes, UniformU, U3dContexts.KnotCountU, U3dContexts.KnotMultiplicityU);
        var knotsV = ReadKnots(ref reader, declaration, attributes, UniformV, U3dContexts.KnotCountV, U3dContexts.KnotMultiplicityV);
        var countU = reader.ReadU32();
        var countV = reader.ReadU32();
        if (reader.Failed || (attributes & IndependentKnots) != 0 || shading >= declaration.Shadings.Length || !PointsFit(countU, countV, degreeU, degreeV, reader.RemainingBytes, builder))
        {
            builder.Problem($"A surface of {declaration.Name} is cut short, has independent knots, or declares more control points than will be decoded.");
            return false;
        }

        var points = ReadPoints(ref reader, declaration, attributes, declaration.Shadings[shading], countU, countV);
        if (reader.Failed)
        {
            builder.Problem($"A surface of {declaration.Name} is cut short.");
            return false;
        }

        var layout = new SurfaceLayout((attributes & Bezier) != 0, (int)degreeU, (int)degreeV, (int)countU, (int)countV, (int)shading);
        var surface = Build(layout, knotsU, knotsV, points);
        if (surface is null)
        {
            builder.Problem($"A surface of {declaration.Name} has knots that do not fit its degree and control points, so it is not drawn.");
            return true;
        }

        builder.Meshes.Add(U3dSurfaceMesher.Mesh(declaration.Name, surface));
        return true;
    }

    /// <summary>Checks that a surface's control points are few enough to decode and could fit in what is left of the block.</summary>
    /// <param name="countU">Control points in the U direction.</param>
    /// <param name="countV">Control points in the V direction.</param>
    /// <param name="degreeU">The U degree.</param>
    /// <param name="degreeV">The V degree.</param>
    /// <param name="remaining">The bytes left in the block.</param>
    /// <param name="builder">Holds the limits.</param>
    /// <returns><see langword="true"/> when the counts and degrees are acceptable.</returns>
    private static bool PointsFit(uint countU, uint countV, uint degreeU, uint degreeV, int remaining, U3dModelBuilder builder)
    {
        var total = (long)countU * countV;
        return degreeU is >= 1 and <= U3dSurfaceBasis.MaxDegree
            && degreeV is >= 1 and <= U3dSurfaceBasis.MaxDegree
            && countU > degreeU
            && countV > degreeV
            && total <= builder.Limits.MaxControlPoints
            && total * PositionBytes <= remaining;
    }

    /// <summary>Reads one direction's knot array in the form the file stores it.</summary>
    /// <param name="reader">The block's reader.</param>
    /// <param name="declaration">The object declaration, which gives the knot precision.</param>
    /// <param name="attributes">The surface attributes.</param>
    /// <param name="uniformFlag">The attribute flag for uniform knots in this direction.</param>
    /// <param name="countContext">The context of the unique knot count.</param>
    /// <param name="multiplicityContext">The context of a knot's multiplicity.</param>
    /// <returns>The knots as written: two values when uniform, otherwise each unique knot with its multiplicity.</returns>
    private static KnotArray ReadKnots(ref U3dBitReader reader, U3dFreeFormDeclaration declaration, uint attributes, uint uniformFlag, uint countContext, uint multiplicityContext)
    {
        if ((attributes & uniformFlag) != 0)
        {
            return new(true, [ReadKnot(ref reader, declaration), ReadKnot(ref reader, declaration)], []);
        }

        var count = reader.ReadCompressedU32(countContext);
        if (reader.Failed || count > reader.RemainingBytes)
        {
            reader.Fail();
            return new(false, [], []);
        }

        var values = new double[count];
        var multiplicities = new int[count];
        for (var i = 0; i < values.Length; i++)
        {
            multiplicities[i] = (int)Math.Min(reader.ReadCompressedU32(multiplicityContext), int.MaxValue);
            values[i] = ReadKnot(ref reader, declaration);
        }

        return new(false, values, multiplicities);
    }

    /// <summary>Reads one knot value in the precision the declaration gives.</summary>
    /// <param name="reader">The block's reader.</param>
    /// <param name="declaration">The object declaration.</param>
    /// <returns>The knot.</returns>
    private static double ReadKnot(ref U3dBitReader reader, U3dFreeFormDeclaration declaration) => declaration.DoubleKnots ? reader.ReadF64() : reader.ReadF32();

    /// <summary>Reads the control points of a surface.</summary>
    /// <param name="reader">The block's reader, placed at the first control point.</param>
    /// <param name="declaration">The object declaration.</param>
    /// <param name="attributes">The surface attributes.</param>
    /// <param name="shading">The shading description, which says which per-point values follow the position.</param>
    /// <param name="countU">Control points in the U direction.</param>
    /// <param name="countV">Control points in the V direction.</param>
    /// <returns>The points as x, y, z and weight.</returns>
    private static Vector4[] ReadPoints(ref U3dBitReader reader, U3dFreeFormDeclaration declaration, uint attributes, U3dShadingList shading, uint countU, uint countV)
    {
        var points = new Vector4[(int)(countU * countV)];
        var rational = (attributes & NonRational) == 0;
        var extras = ExtraValues(shading);
        for (var i = 0; i < points.Length; i++)
        {
            var position = reader.ReadVector3();
            for (var extra = 0; extra < extras; extra++)
            {
                _ = reader.ReadF32();
            }

            points[i] = new(position, rational ? (float)ReadWeight(ref reader, declaration) : 1);
        }

        return points;
    }

    /// <summary>Reads a control point weight in the precision the declaration gives.</summary>
    /// <param name="reader">The block's reader.</param>
    /// <param name="declaration">The object declaration.</param>
    /// <returns>The weight.</returns>
    private static double ReadWeight(ref U3dBitReader reader, U3dFreeFormDeclaration declaration) => declaration.DoubleWeights ? reader.ReadF64() : reader.ReadF32();

    /// <summary>Counts the 32-bit values a control point carries after its position: colours and texture coordinates.</summary>
    /// <param name="shading">The shading description.</param>
    /// <returns>The count.</returns>
    private static int ExtraValues(U3dShadingList shading)
    {
        var count = (shading.UsesDiffuse ? ColorValues : 0) + (shading.UsesSpecular ? ColorValues : 0);
        foreach (var dimensions in shading.LayerDimensions)
        {
            count += (int)Math.Min(dimensions, MaxDimensions);
        }

        return count;
    }

    /// <summary>Expands the knots into a surface, checking that they fit the degree and the control points.</summary>
    /// <param name="layout">The kind, degrees, counts and shader list of the surface.</param>
    /// <param name="knotsU">The U knots as written.</param>
    /// <param name="knotsV">The V knots as written.</param>
    /// <param name="points">The control points.</param>
    /// <returns>The surface, or <see langword="null"/> when the knots are not valid.</returns>
    private static U3dSurface? Build(SurfaceLayout layout, KnotArray knotsU, KnotArray knotsV, Vector4[] points)
    {
        var u = knotsU.Expand(layout.DegreeU, layout.CountU, layout.IsBezier);
        var v = knotsV.Expand(layout.DegreeV, layout.CountV, layout.IsBezier);
        return u is null || v is null ? null : new(layout.IsBezier, layout.DegreeU, layout.DegreeV, u, v, layout.CountU, layout.CountV, points, layout.Shading);
    }

    /// <summary>The kind, degrees, counts and shader list of a surface.</summary>
    /// <param name="IsBezier"><see langword="true"/> for a Bezier surface.</param>
    /// <param name="DegreeU">The U degree.</param>
    /// <param name="DegreeV">The V degree.</param>
    /// <param name="CountU">Control points in the U direction.</param>
    /// <param name="CountV">Control points in the V direction.</param>
    /// <param name="Shading">The shader list index.</param>
    private readonly record struct SurfaceLayout(bool IsBezier, int DegreeU, int DegreeV, int CountU, int CountV, int Shading);

    /// <summary>One direction's knots as the file writes them.</summary>
    /// <param name="IsUniform"><see langword="true"/> when only the lowest and highest knot are stored.</param>
    /// <param name="Values">The two bounds when uniform, otherwise the unique knots.</param>
    /// <param name="Multiplicities">How many times each unique knot repeats; empty when uniform.</param>
    private sealed record KnotArray(bool IsUniform, double[] Values, int[] Multiplicities)
    {
        /// <summary>Writes the knots out in full and checks them.</summary>
        /// <param name="degree">The degree.</param>
        /// <param name="count">The number of control points.</param>
        /// <param name="bezier"><see langword="true"/> for a Bezier direction.</param>
        /// <returns>The knot vector, or <see langword="null"/> when the knots are out of order, repeat too often, or are the wrong number.</returns>
        internal double[]? Expand(int degree, int count, bool bezier)
        {
            var expected = bezier ? (((count - 1) / degree) + 1) : count + degree + 1;
            return IsUniform ? ExpandUniform(degree, count, bezier) : ExpandListed(degree, bezier, expected);
        }

        /// <summary>Writes out uniform knots: the bounds repeated at the ends and equal spacing between them.</summary>
        /// <param name="degree">The degree.</param>
        /// <param name="count">The number of control points.</param>
        /// <param name="bezier"><see langword="true"/> for a Bezier direction.</param>
        /// <returns>The knots, or <see langword="null"/> when the bounds are not increasing.</returns>
        private double[]? ExpandUniform(int degree, int count, bool bezier)
        {
            if (Values.Length != 2 || !(Values[0] < Values[1]))
            {
                return null;
            }

            var pieces = bezier ? (count - 1) / degree : count - degree;
            var knots = new List<double>();
            var step = (Values[1] - Values[0]) / pieces;
            for (var i = 0; i <= pieces; i++)
            {
                var value = i == pieces ? Values[1] : Values[0] + (step * i);
                var repeats = !bezier && (i == 0 || i == pieces) ? degree + 1 : 1;
                for (var repeat = 0; repeat < repeats; repeat++)
                {
                    knots.Add(value);
                }
            }

            return [.. knots];
        }

        /// <summary>Writes out listed knots, repeating each by its multiplicity.</summary>
        /// <param name="degree">The degree.</param>
        /// <param name="bezier"><see langword="true"/> for a Bezier direction.</param>
        /// <param name="expected">The number of knots the degree and control points need.</param>
        /// <returns>The knots, or <see langword="null"/> when they are not valid.</returns>
        private double[]? ExpandListed(int degree, bool bezier, int expected)
        {
            var knots = new List<double>();
            for (var i = 0; i < Values.Length; i++)
            {
                if (Multiplicities[i] < 1 || Multiplicities[i] > (bezier ? 1 : degree + 1) || (i > 0 && !(Values[i] > Values[i - 1])) || knots.Count + Multiplicities[i] > expected)
                {
                    return null;
                }

                for (var repeat = 0; repeat < Multiplicities[i]; repeat++)
                {
                    knots.Add(Values[i]);
                }
            }

            return knots.Count == expected ? [.. knots] : null;
        }
    }
}
