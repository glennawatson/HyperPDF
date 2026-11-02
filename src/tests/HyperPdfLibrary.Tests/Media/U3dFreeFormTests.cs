// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Media.U3d;
using static HyperPdfLibrary.Tests.Media.U3dFramePixels;
using static HyperPdfLibrary.Tests.Media.U3dTestScenes;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Checks the free-form curve and surface extension: surfaces of every kind are decoded, evaluated and drawn, and damaged ones are refused.</summary>
public sealed class U3dFreeFormTests
{
    /// <summary>The name of the free-form object.</summary>
    private const string Patch = "Patch";

    /// <summary>The grid point that holds the middle of the surface in the U direction.</summary>
    private const int MiddleRow = U3dSurfaceMesher.Steps / 2;

    /// <summary>The vertices in each direction of the grid.</summary>
    private const int Side = U3dSurfaceMesher.Steps + 1;

    /// <summary>The width and height of the test frame.</summary>
    private const int Frame = 64;

    /// <summary>The centre pixel of the test frame.</summary>
    private const int Centre = 32;

    /// <summary>The bytes of a block's type and two sizes, which come before its data.</summary>
    private const int FrameBytes = 12;

    /// <summary>Two: the triangles in a grid cell, and the divisor that halves a block.</summary>
    private const int HalfDivisor = 2;

    /// <summary>The precision flags for 64-bit knots and weights.</summary>
    private const uint BothDoubles = 3;

    /// <summary>The fewest red pixels the patch covers.</summary>
    private const int LeastPixels = 440;

    /// <summary>The most red pixels the patch covers.</summary>
    private const int MostPixels = 540;

    /// <summary>The sine and cosine of 45 degrees, the weight of the middle of a quarter circle.</summary>
    private const float Cosine45 = 0.70710678F;

    /// <summary>The room allowed for floating point rounding.</summary>
    private const float Tolerance = 0.001F;

    /// <summary>One half.</summary>
    private const float Half = 0.5F;

    /// <summary>The degree of a quadratic surface.</summary>
    private const uint Quadratic = 2;

    /// <summary>The control points along a quadratic direction.</summary>
    private const uint Three = 3;

    /// <summary>The control points of the linear B-spline test.</summary>
    private const uint Four = 4;

    /// <summary>The multiplicity of the end knots of a quadratic B-spline.</summary>
    private const uint QuadraticEnds = 3;

    /// <summary>The last knot of the linear B-spline test.</summary>
    private const float LastKnot = 3;

    /// <summary>The number of surfaces the tests declare when they need more than one.</summary>
    private const uint TwoSurfaces = 2;

    /// <summary>A degree no surface may have.</summary>
    private const uint TooHigh = 40;

    /// <summary>A count no block can hold.</summary>
    private const uint HugeCount = 4_000_000_000;

    /// <summary>A flat bilinear Bezier patch in the XZ plane is decoded, meshed and drawn like the quad it is.</summary>
    /// <param name="compressed">Whether the file is written as an arithmetic code.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task DrawsABezierPatch(bool compressed)
    {
        var model = U3dModelReader.Read(File(compressed, 0, FlatPatch()));

        var frame = U3dRenderer.Render(model, new(Frame, Frame, null, new(0, 0, 1, 1)), CancellationToken.None);

        await Assert.That(model.IsComplete).IsTrue();
        await Assert.That(model.Meshes.Single().Name).IsEqualTo(Patch);
        await Assert.That(model.Meshes.Single().Faces.Length).IsEqualTo(U3dSurfaceMesher.Steps * U3dSurfaceMesher.Steps * HalfDivisor);
        await Assert.That(Pixel(frame, Centre, Centre)).IsEqualTo(Red);
        await Assert.That(Count(frame, Red)).IsBetween(LeastPixels, MostPixels);
    }

    /// <summary>A quadratic Bezier curve extruded into a surface passes through the point the Bernstein polynomials give.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EvaluatesAQuadraticBezierSurface()
    {
        var mesh = U3dModelReader.Read(File(false, 0, Parabola(true))).Meshes.Single();

        Vector3 expected = new(0, Half, 0);
        await Assert.That(Vector3.Distance(mesh.Positions[MiddleRow * Side], expected)).IsLessThan(Tolerance);
        await Assert.That(Vector3.Distance(mesh.Positions[(MiddleRow * Side) + Side - 1], new(0, Half, 1))).IsLessThan(Tolerance);
    }

    /// <summary>A quadratic B-spline with end knots repeated gives the same curve as the Bezier surface, with listed and uniform knots.</summary>
    /// <param name="uniform">Whether the knots are stored as two bounds.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EvaluatesAQuadraticBSpline(bool uniform)
    {
        var spec = Parabola(false) with
        {
            UniformU = uniform,
            MultiplicitiesU = uniform ? [] : [QuadraticEnds, QuadraticEnds],
        };

        var mesh = U3dModelReader.Read(File(false, 0, spec)).Meshes.Single();

        await Assert.That(Vector3.Distance(mesh.Positions[MiddleRow * Side], new(0, Half, 0))).IsLessThan(Tolerance);
    }

    /// <summary>A linear B-spline with uniform knots passes halfway between its middle control points halfway along its range.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EvaluatesAUniformLinearBSpline()
    {
        var spec = Parabola(false) with
        {
            DegreeU = 1,
            CountU = Four,
            UniformU = true,
            KnotsU = [0, LastKnot],
            MultiplicitiesU = [],
            Points = [new(-1, 0, 0, 1), new(-1, 0, 1, 1), new(0, 0, 0, 1), new(0, 0, 1, 1), new(1, 0, 0, 1), new(1, 0, 1, 1), new(1, 0, 0, 1), new(1, 0, 1, 1)],
        };

        var mesh = U3dModelReader.Read(File(false, 0, spec)).Meshes.Single();

        await Assert.That(Vector3.Distance(mesh.Positions[MiddleRow * Side], new(Half, 0, 0))).IsLessThan(Tolerance);
    }

    /// <summary>A rational quadratic Bezier arc has the weighted point of a circle at its middle, with weights stored as floats or doubles.</summary>
    /// <param name="doubles">Whether knots and weights are stored as 64-bit values.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EvaluatesARationalArc(bool doubles)
    {
        var spec = Parabola(true) with
        {
            IsRational = true,
            Points = [new(1, 0, 0, 1), new(1, 0, 1, 1), new(1, 1, 0, Cosine45), new(1, 1, 1, Cosine45), new(0, 1, 0, 1), new(0, 1, 1, 1)],
        };

        var mesh = U3dModelReader.Read(File(false, doubles ? BothDoubles : 0U, spec)).Meshes.Single();

        await Assert.That(Vector3.Distance(mesh.Positions[MiddleRow * Side], new(Cosine45, Cosine45, 0))).IsLessThan(Tolerance);
    }

    /// <summary>Surfaces that are cut short, have independent knots or an unknown shader list are refused with a reason, and later surfaces are not read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesSurfacesItCannotRead()
    {
        var independent = Decode(FlatPatch() with { IndependentKnots = true });
        var shading = Decode(FlatPatch() with { Shading = Three });
        var tooHigh = Decode(FlatPatch() with { DegreeU = TooHigh });
        var zeroDegree = Decode(FlatPatch() with { DegreeV = 0 });
        var tooFew = Decode(FlatPatch() with { CountU = 1 });
        var huge = Decode(FlatPatch() with { CountU = HugeCount, CountV = HugeCount });

        foreach (var model in new[] { independent, shading, tooHigh, zeroDegree, tooFew, huge })
        {
            await Assert.That(model.Meshes).IsEmpty();
            await Assert.That(Array.Exists(model.Problems, static p => p.Contains("independent knots", StringComparison.Ordinal))).IsTrue();
        }
    }

    /// <summary>Knots that repeat too often, are out of order, are the wrong number or have bounds that do not increase leave the surface out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesBadKnots()
    {
        var repeats = Decode(FlatPatch() with { KnotsU = [0, 1], MultiplicitiesU = [Three, Three] });
        var zeroRepeat = Decode(FlatPatch() with { KnotsU = [0, 1], MultiplicitiesU = [0, 1] });
        var unordered = Decode(FlatPatch() with { KnotsU = [1, 0] });
        var wrongCount = Decode(FlatPatch() with { KnotsU = [0, 1, LastKnot], MultiplicitiesU = [1, 1, 1] });
        var reversed = Decode(FlatPatch() with { IsBezier = false, UniformU = true, KnotsU = [1, 0] });
        var oneBound = Decode(FlatPatch() with { IsBezier = false, UniformU = true, KnotsU = [0, 0] });

        foreach (var model in new[] { repeats, zeroRepeat, unordered, wrongCount, reversed, oneBound })
        {
            await Assert.That(model.Meshes).IsEmpty();
            await Assert.That(Array.Exists(model.Problems, static p => p.Contains("knots that do not fit", StringComparison.Ordinal))).IsTrue();
        }
    }

    /// <summary>A continuation that names a missing object or surfaces the declaration does not have is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesBadContinuations()
    {
        var writer = new U3dFileWriter(false);
        var declared = writer.Chain(Patch, 1, writer.FreeFormObject(Patch, 1, 0));
        var unknown = writer.FreeFormSurfaces("Nobody", 0, FlatPatch());
        var tooMany = writer.FreeFormSurfaces(Patch, 1, FlatPatch());

        var model = U3dModelReader.Read(new U3dFileWriter(false).Add(writer.FreeFormType()).Add(declared).Add(unknown).Add(tooMany).ToArray());

        await Assert.That(model.Meshes).IsEmpty();
        await Assert.That(Array.Exists(model.Problems, static p => p.Contains("does not declare", StringComparison.Ordinal))).IsTrue();
        await Assert.That(Array.Exists(model.Problems, static p => p.Contains("names surfaces", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>A surface block cut in the middle, and a declaration that is cut short or too large, are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesDamagedBlocks()
    {
        var writer = new U3dFileWriter(false);
        var good = writer.FreeFormSurfaces(Patch, 0, FlatPatch());
        var cut = U3dFileWriter.Block(U3dFileWriter.FreeFormContinuation, good.AsSpan(FrameBytes, good.Length / HalfDivisor).ToArray());
        var shortDeclaration = U3dFileWriter.Block(U3dFileWriter.FreeFormDeclaration, new U3dValueWriter(true).String("Short").ToArray());
        var hugeDeclaration = U3dFileWriter.Block(
            U3dFileWriter.FreeFormDeclaration,
            new U3dValueWriter(true).String("Huge").U32(0).U32(0).U32(HugeCount).U32(0).U32(0).U32(0).ToArray());
        var file = new U3dFileWriter(false)
            .Add(writer.FreeFormType())
            .Add(writer.Chain(Patch, 1, writer.FreeFormObject(Patch, 1, 0)))
            .Add(cut)
            .Add(shortDeclaration)
            .Add(hugeDeclaration)
            .ToArray();

        var model = U3dModelReader.Read(file);

        await Assert.That(model.Meshes).IsEmpty();
        await Assert.That(model.Problems.Length >= Three).IsTrue();
    }

    /// <summary>A free-form object that declares faces notes that trim curves are not applied, and several surfaces in one block are all meshed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesTrimCurvesAndReadsSeveralSurfaces()
    {
        var writer = new U3dFileWriter(false);
        var file = writer
            .Add(writer.FreeFormType())
            .Add(writer.Chain(Patch, 1, writer.FreeFormObject(Patch, TwoSurfaces, 1)))
            .Add(writer.FreeFormSurfaces(Patch, 0, FlatPatch(), FlatPatch()))
            .ToArray();

        var model = U3dModelReader.Read(file);

        await Assert.That(model.Meshes.Length).IsEqualTo((int)TwoSurfaces);
        await Assert.That(model.Problems.Single()).Contains("trim curves");
    }

    /// <summary>Extensions that are not the free-form one, and a free-form type that is not a model resource or has no continuation, are noted and not decoded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesOtherExtensions()
    {
        var writer = new U3dFileWriter(false);
        var other = U3dFileWriter.Block(0xFFFFFF16, new U3dValueWriter(true).String("Other").U32(1).U32(1).U16(1).U16(1).U8(1).U8(1).U8(1).U8(1).U8(1).U8(1).U8(1).U8(1).U32(0x105).U32(0).ToArray());
        var cut = U3dFileWriter.Block(0xFFFFFF16, new U3dValueWriter(true).String("Cut").ToArray());
        var noContinuation = U3dFileWriter.Block(0xFFFFFF16, FreeFormTypeBytes(1, 0));
        var wrongChain = U3dFileWriter.Block(0xFFFFFF16, FreeFormTypeBytes(0, 1));
        var file = writer.Add(other).Add(cut).Add(noContinuation).Add(wrongChain).ToArray();

        var model = U3dModelReader.Read(file);

        await Assert.That(model.Meshes).IsEmpty();
        await Assert.That(Array.Exists(model.Problems, static p => p.Contains("extension (Other)", StringComparison.Ordinal))).IsTrue();
        await Assert.That(Array.Exists(model.Problems, static p => p.Contains("extension (Free-Form Object)", StringComparison.Ordinal))).IsTrue();
        await Assert.That(Array.Exists(model.Problems, static p => p.Contains("New Object Type block is cut short", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>The faces of the meshed patch cover the grid with consistent corner indices.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeshesCoverTheGrid()
    {
        var mesh = Decode(FlatPatch()).Meshes.Single();

        await Assert.That(mesh.Positions.Length).IsEqualTo(Side * Side);
        await Assert.That(mesh.Normals.Length).IsEqualTo(Side * Side);
        await Assert.That(mesh.TextureCoordinates.Length).IsEqualTo(Side * Side);
        await Assert.That(Array.TrueForAll(mesh.Faces, static face => face.A.Position == face.A.Normal && face.A.Position == face.A.TextureCoordinate)).IsTrue();
        await Assert.That(mesh.IsComplete).IsTrue();
    }

    /// <summary>Makes a flat bilinear Bezier patch in the XZ plane.</summary>
    /// <returns>The surface.</returns>
    private static U3dSurfaceSpec FlatPatch() => new(
        true,
        false,
        1,
        1,
        [0, 1],
        [1, 1],
        [0, 1],
        [1, 1],
        false,
        false,
        Quadratic,
        Quadratic,
        [new(-1, 0, -1, 1), new(-1, 0, 1, 1), new(1, 0, -1, 1), new(1, 0, 1, 1)],
        0,
        false);

    /// <summary>Makes a quadratic curve in U extruded one unit along Z in V.</summary>
    /// <param name="bezier"><see langword="true"/> for a Bezier surface, <see langword="false"/> for a B-spline surface.</param>
    /// <returns>The surface.</returns>
    private static U3dSurfaceSpec Parabola(bool bezier) => new(
        bezier,
        false,
        Quadratic,
        1,
        [0, 1],
        bezier ? [1, 1] : [QuadraticEnds, QuadraticEnds],
        [0, 1],
        bezier ? [1, 1] : [Quadratic, Quadratic],
        false,
        false,
        Three,
        Quadratic,
        [new(-1, 0, 0, 1), new(-1, 0, 1, 1), new(0, 1, 0, 1), new(0, 1, 1, 1), new(1, 0, 0, 1), new(1, 0, 1, 1)],
        0,
        false);

    /// <summary>Writes a file holding a free-form object and its surfaces.</summary>
    /// <param name="compressed">Whether the file is written as an arithmetic code.</param>
    /// <param name="attributes">The precision flags of the declaration.</param>
    /// <param name="surfaces">The surfaces.</param>
    /// <returns>The file.</returns>
    private static byte[] File(bool compressed, uint attributes, params U3dSurfaceSpec[] surfaces)
    {
        var writer = new U3dFileWriter(compressed) { FreeFormAttributes = attributes };
        return writer
            .Add(writer.FreeFormType())
            .Add(writer.Chain(Patch, 1, writer.FreeFormObject(Patch, (uint)surfaces.Length, 0)))
            .Add(writer.FreeFormSurfaces(Patch, 0, surfaces))
            .WithRedShader()
            .WithNode(Patch, BothSides, Root)
            .WithAmbient()
            .WithCamera()
            .ToArray();
    }

    /// <summary>Decodes a file holding one surface.</summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The model.</returns>
    private static U3dModel Decode(U3dSurfaceSpec surface) => U3dModelReader.Read(File(false, 0, surface));

    /// <summary>Writes the data of a New Object Type block that names the free-form extension.</summary>
    /// <param name="chain">The modifier type.</param>
    /// <param name="continuations">The number of continuation block types.</param>
    /// <returns>The data.</returns>
    private static byte[] FreeFormTypeBytes(uint chain, uint continuations)
    {
        var writer = new U3dValueWriter(true).String("Free-Form Object").U32(chain).U32(0x095467DF).U16(0xEE24).U16(0x4C59)
            .U8(0x9A).U8(0xA5).U8(0x76).U8(0x8E).U8(0x39).U8(0xE2).U8(0x68).U8(0xDE).U32(0x101).U32(continuations);
        for (uint i = 0; i < continuations; i++)
        {
            _ = writer.U32(0x102);
        }

        return writer.ToArray();
    }
}
