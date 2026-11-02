// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Media.U3d;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Checks the decoder against blocks written by hand to be cut short, to declare counts they cannot hold, or to use rarely seen fields.</summary>
public sealed class U3dHostileInputTests
{
    /// <summary>The name of the coloured surface.</summary>
    private const string ColouredName = "Coloured";

    /// <summary>The name of the multi-piece surface.</summary>
    private const string PiecesName = "Pieces";

    /// <summary>The name of the surface that is cut short.</summary>
    private const string TruncatedName = "Truncated";

    /// <summary>Half way along a unit span.</summary>
    private const float Halfway = 0.5F;

    /// <summary>The bytes of a block's type and sizes, which come before its data.</summary>
    private const int FrameBytes = 12;

    /// <summary>A count no block can hold.</summary>
    private const uint HugeCount = 4_000_000_000;

    /// <summary>A string length that runs past the data after it.</summary>
    private const ushort LongName = 500;

    /// <summary>Bytes of padding that make a block look big enough for its counts.</summary>
    private const int Padding = 140;

    /// <summary>More faces than the structure reader will read.</summary>
    private const uint ManyFaces = 60_000_000;

    /// <summary>The block type of a mesh declaration.</summary>
    private const uint MeshType = 0xFFFFFF31;

    /// <summary>The block type of a base mesh.</summary>
    private const uint BaseType = 0xFFFFFF3B;

    /// <summary>The block type of a model node.</summary>
    private const uint ModelNodeType = 0xFFFFFF22;

    /// <summary>The block type of a view node.</summary>
    private const uint ViewNodeType = 0xFFFFFF24;

    /// <summary>The block type of a view resource.</summary>
    private const uint ViewResourceType = 0xFFFFFF52;

    /// <summary>The block type of a lit texture shader.</summary>
    private const uint ShaderType = 0xFFFFFF53;

    /// <summary>The block type of a shading modifier.</summary>
    private const uint ShadingType = 0xFFFFFF45;

    /// <summary>The shading attribute for per-vertex diffuse colour.</summary>
    private const uint DiffuseShading = 1;

    /// <summary>The shading attribute for per-vertex specular colour.</summary>
    private const uint SpecularShading = 2;

    /// <summary>The surface attributes of a non-rational Bezier surface.</summary>
    private const uint BezierPlain = 3;

    /// <summary>The surface attribute flag for Bezier.</summary>
    private const uint BezierOnly = 2;

    /// <summary>The control points along one direction.</summary>
    private const uint Two = 2;

    /// <summary>The control points of the three-point test.</summary>
    private const uint Three = 3;

    /// <summary>The grid row halfway between the second and third control points of a three-point surface.</summary>
    private const int ThreeQuarterRow = 12;

    /// <summary>The vertices in each direction of a meshed surface.</summary>
    private const int Side = U3dSurfaceMesher.Steps + 1;

    /// <summary>The rounding room.</summary>
    private const float Tolerance = 0.001F;

    /// <summary>The histogram total at which counts halve, with room.</summary>
    private const uint HalvingRoom = 0x2000;

    /// <summary>The values added to a histogram to make it halve.</summary>
    private const int ManyValues = 9_000;

    /// <summary>The first symbol a histogram does not count.</summary>
    private const uint UncountedSymbol = 0xFFFF;

    /// <summary>The symbols of the repeated pattern in the halving round trip.</summary>
    private const uint Pattern = 6;

    /// <summary>The profile bytes of a header with nothing in it.</summary>
    private const int ShortHeaderBytes = 4;

    /// <summary>A shape declaration with more faces than the structure reader will read is noted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesHugeShapes()
    {
        var data = new U3dValueWriter(true).String("Big").U32(0).U32(0).U32(ManyFaces).U32(0).U32(0).U32(0).U32(0).U32(0).U32(0).ToArray();
        var file = new U3dFileWriter(false).Add(U3dFileWriter.Block(MeshType, data)).ToArray();

        var scene = U3dReader.Read(file);

        await Assert.That(Array.Exists(scene.Problems, static p => p.Contains("more than", StringComparison.Ordinal))).IsTrue();
        await Assert.That(U3dReader.MaxChainModifiers).IsGreaterThan(0);
    }

    /// <summary>A file header cut short is noted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesAShortHeader()
    {
        var file = U3dFileWriter.Block(0x00443355, new byte[ShortHeaderBytes]);

        var scene = U3dReader.Read(file);

        await Assert.That(Array.Exists(scene.Problems, static p => p.Contains("header is cut short", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Surface fields that are rarely used decode: control points with colours and texture coordinates, and a surface with several pieces.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesColouredControlPoints()
    {
        var declaration = U3dFileWriter.Block(
            U3dFileWriter.FreeFormDeclaration,
            new U3dValueWriter(true).String(ColouredName).U32(0).U32(0).U32(1).U32(0).U32(0).U32(1).U32(DiffuseShading).U32(1).U32(Two).U32(0).U32(0).ToArray());
        var surface = new U3dValueWriter(true).String(ColouredName).U32(0).U32(0).U32(1).U32(BezierPlain).U32(0).U32(1).U32(1);
        WriteUnitKnots(surface);
        WriteUnitKnots(surface);
        _ = surface.U32(Two).U32(Two);
        for (var point = 0; point < Two * Two; point++)
        {
            _ = surface.Vector3(Vector3.UnitX).Vector4(Vector4.One).F32(0).F32(1);
        }

        var writer = new U3dFileWriter(false);
        var file = new U3dFileWriter(false)
            .Add(writer.FreeFormType())
            .Add(writer.Chain(ColouredName, 1, declaration))
            .Add(U3dFileWriter.Block(U3dFileWriter.FreeFormContinuation, surface.ToArray()))
            .ToArray();

        var model = U3dModelReader.Read(file);

        await Assert.That(model.Meshes.Single().Positions[0]).IsEqualTo(Vector3.UnitX);
    }

    /// <summary>A Bezier surface of several pieces is evaluated in the piece that holds each parameter.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EvaluatesAMultiPieceBezierSurface()
    {
        var spec = new U3dSurfaceSpec(
            true,
            false,
            1,
            1,
            [0, 1, Two],
            [1, 1, 1],
            [0, 1],
            [1, 1],
            false,
            false,
            Three,
            Two,
            [new(-1, 0, 0, 1), new(-1, 0, 1, 1), new(0, 0, 0, 1), new(0, 0, 1, 1), new(1, 0, 0, 1), new(1, 0, 1, 1)],
            0,
            false);
        var writer = new U3dFileWriter(false);
        var file = writer
            .Add(writer.FreeFormType())
            .Add(writer.Chain(PiecesName, 1, writer.FreeFormObject(PiecesName, 1, 0)))
            .Add(writer.FreeFormSurfaces(PiecesName, 0, spec))
            .ToArray();

        var mesh = U3dModelReader.Read(file).Meshes.Single();

        Vector3 expected = new(Halfway, 0, 0);
        await Assert.That(Vector3.Distance(mesh.Positions[ThreeQuarterRow * Side], expected)).IsLessThan(Tolerance);
    }

    /// <summary>A surface whose knot count is more than its block can hold, or whose weights are cut off, is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesSurfacesWithHugeKnotsOrNoWeights()
    {
        var writer = new U3dFileWriter(false);
        var declaration = writer.Chain(TruncatedName, 1, writer.FreeFormObject(TruncatedName, 1, 0));
        var huge = new U3dValueWriter(true).String(TruncatedName).U32(0).U32(0).U32(1).U32(BezierOnly).U32(0).U32(1).U32(1).U32(HugeCount);
        for (var i = 0; i < Padding; i++)
        {
            _ = huge.U8(0);
        }

        var rational = new U3dSurfaceSpec(true, true, 1, 1, [0, 1], [1, 1], [0, 1], [1, 1], false, false, Two, Two, [new(0, 0, 0, 1), new(0, 0, 1, 1), new(1, 0, 0, 1), new(1, 0, 1, 1)], 0, false);
        var whole = writer.FreeFormSurfaces(TruncatedName, 0, rational);
        var noWeights = U3dFileWriter.Block(U3dFileWriter.FreeFormContinuation, whole.AsSpan(FrameBytes, whole.Length - FrameBytes - (int)(Two * Two * sizeof(float))).ToArray());
        var file = new U3dFileWriter(false)
            .Add(writer.FreeFormType())
            .Add(declaration)
            .Add(U3dFileWriter.Block(U3dFileWriter.FreeFormContinuation, huge.ToArray()))
            .Add(noWeights)
            .ToArray();

        var model = U3dModelReader.Read(file);

        await Assert.That(model.Meshes).IsEmpty();
        await Assert.That(string.Join('|', model.Problems)).Contains("independent knots");
    }

    /// <summary>Faces that carry specular colour indices are read, and a face that needs a colour the mesh does not have is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsSpecularIndicesAndRefusesMissingColours()
    {
        var specular = Mesh("Spec", 1, SpecularShading);
        var diffuse = Mesh("Dif", 0, DiffuseShading);

        var model = U3dModelReader.Read(new U3dFileWriter(false).Add(specular.Declaration).Add(specular.Base).Add(diffuse.Declaration).Add(diffuse.Base).ToArray());

        await Assert.That(model.Meshes.Single().Name).IsEqualTo("Spec");
        await Assert.That(model.Problems.Length).IsGreaterThan(0);
    }

    /// <summary>A node that is cut short, a view with more backdrops than it can hold, and shader and view resources that are cut short are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesCutResourcesAndNodes()
    {
        var model = new U3dValueWriter(true).String("Model").U32(0).ToArray();
        var view = new U3dValueWriter(true).String("View").U32(0).String("R").U32(0).F32(1).F32(1).F32(1).Vector4(Vector4.One).U32(HugeCount).ToArray();
        var resource = Cut(new U3dValueWriter(true).String("V").U32(1));
        var shader = Cut(new U3dValueWriter(true).String("S").U32(0).F32(0).U32(0).U32(0).U32(0).U32(1).U32(0).String("M"));
        var shading = Cut(new U3dValueWriter(true).String("N").U32(0).U32(0).U32(1).U32(1));
        var lists = new U3dValueWriter(true).String("N").U32(0).U32(0).U32(1).U32(HugeCount).ToArray();
        var file = new U3dFileWriter(false)
            .Add(U3dFileWriter.Block(ModelNodeType, model))
            .Add(U3dFileWriter.Block(ViewNodeType, view))
            .Add(U3dFileWriter.Block(ViewResourceType, resource))
            .Add(U3dFileWriter.Block(ShaderType, shader))
            .Add(U3dFileWriter.Block(ShadingType, shading))
            .Add(U3dFileWriter.Block(ShadingType, lists))
            .ToArray();

        var result = U3dModelReader.Read(file);

        await Assert.That(result.Nodes).IsEmpty();
        await Assert.That(result.ViewResources).IsEmpty();
        await Assert.That(result.Shaders).IsEmpty();
        await Assert.That(result.Shadings).IsEmpty();
        await Assert.That(Array.Exists(result.Problems, static p => p.Contains("node block is cut short", StringComparison.Ordinal))).IsTrue();
        await Assert.That(Array.Exists(result.Problems, static p => p.Contains("view resource is cut short", StringComparison.Ordinal))).IsTrue();
        await Assert.That(Array.Exists(result.Problems, static p => p.Contains("shader is cut short", StringComparison.Ordinal))).IsTrue();
        await Assert.That(Array.Exists(result.Problems, static p => p.Contains("shading modifier is cut short", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>A shading description with more layers than the block can hold is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesHugeShadingLayers()
    {
        var data = new U3dValueWriter(true).String("S").U32(0).U32(0).U32(0).U32(0).U32(0).U32(0).U32(0).U32(0).U32(1).U32(0).U32(HugeCount).U32(0).U32(0).ToArray();

        var model = U3dModelReader.Read(new U3dFileWriter(false).Add(U3dFileWriter.Block(MeshType, data)).ToArray());

        await Assert.That(Array.Exists(model.Problems, static p => p.Contains("declaration is cut short", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>A histogram halves its counts when the total gets large, and leaves out symbols it cannot count.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HistogramHalvesAndIgnoresHugeSymbols()
    {
        var histogram = new U3dHistogram();
        for (var i = 0; i < ManyValues; i++)
        {
            histogram.Add(1);
        }

        var total = histogram.Total;
        histogram.Add(UncountedSymbol);
        histogram.Add(uint.MaxValue);

        await Assert.That(total).IsLessThan(HalvingRoom);
        await Assert.That(histogram.Total).IsEqualTo(total);
        await Assert.That(histogram.SymbolAt(0)).IsEqualTo(0U);
        await Assert.That(histogram.SymbolAt(uint.MaxValue)).IsEqualTo(0U);
    }

    /// <summary>A long run of one repeated adaptive value is read back after the histogram has halved several times.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsBackAcrossHalving()
    {
        var contexts = new U3dWriterContexts();
        var writer = new U3dValueWriter(false, contexts);
        for (uint i = 0; i < ManyValues; i++)
        {
            _ = writer.Adaptive(U3dWriterContexts.Shading, i % Pattern);
        }

        var mismatches = CountMismatches(writer.ToArray());

        await Assert.That(mismatches).IsEqualTo(0);
    }

    /// <summary>Counts the values of the halving run that do not read back.</summary>
    /// <param name="bytes">The written data.</param>
    /// <returns>The number of mismatches.</returns>
    private static int CountMismatches(byte[] bytes)
    {
        var reader = new U3dBitReader(bytes, new());
        var mismatches = 0;
        for (uint i = 0; i < ManyValues; i++)
        {
            mismatches += reader.ReadCompressedU32(U3dContexts.Shading) == i % Pattern ? 0 : 1;
        }

        return mismatches;
    }

    /// <summary>Writes the knots of a linear Bezier direction: two unique knots, 0 and 1.</summary>
    /// <param name="writer">The writer.</param>
    private static void WriteUnitKnots(U3dValueWriter writer) => writer.U32(Two).U32(1).F32(0).U32(1).F32(1);

    /// <summary>Adds the padding that makes a block look big enough for its counts after a cut-short field.</summary>
    /// <param name="writer">The writer holding the start of the block.</param>
    /// <returns>The data: the start, a string length that runs past the data, and padding.</returns>
    private static byte[] Cut(U3dValueWriter writer)
    {
        _ = writer.U16(LongName);
        for (var i = 0; i < Padding; i++)
        {
            _ = writer.U8(0);
        }

        return writer.ToArray();
    }

    /// <summary>Writes a mesh declaration and base mesh with one face and one colour kind.</summary>
    /// <param name="name">The mesh's name.</param>
    /// <param name="specularColors">The number of specular colours.</param>
    /// <param name="shading">The shading attributes: 1 diffuse, 2 specular.</param>
    /// <returns>The declaration and base mesh blocks.</returns>
    private static MeshBlocks Mesh(string name, uint specularColors, uint shading)
    {
        var declaration = new U3dValueWriter(true).String(name).U32(0).U32(1).U32(1).U32(Three).U32(0).U32(0).U32(specularColors).U32(0).U32(1).U32(shading).U32(0).U32(0).U32(Three).U32(Three);
        for (var i = 0; i < Three; i++)
        {
            _ = declaration.U32(0);
        }

        for (var i = 0; i < Two + Three; i++)
        {
            _ = declaration.F32(0);
        }

        _ = declaration.F32(0).F32(0).F32(0).U32(0);
        var data = new U3dValueWriter(true).String(name).U32(0).U32(1).U32(Three).U32(0).U32(0).U32(specularColors).U32(0);
        _ = data.Vector3(Vector3.Zero).Vector3(Vector3.UnitX).Vector3(Vector3.UnitZ);
        if (specularColors != 0)
        {
            _ = data.Vector4(Vector4.One);
        }

        _ = data.U32(0);
        for (uint corner = 0; corner < Three; corner++)
        {
            _ = data.U32(corner).U32(0);
        }

        return new(U3dFileWriter.Block(MeshType, declaration.ToArray()), U3dFileWriter.Block(BaseType, data.ToArray()));
    }

    /// <summary>A hand-written mesh declaration and its base mesh.</summary>
    /// <param name="Declaration">The declaration block.</param>
    /// <param name="Base">The base mesh block.</param>
    private sealed record MeshBlocks(byte[] Declaration, byte[] Base);
}
