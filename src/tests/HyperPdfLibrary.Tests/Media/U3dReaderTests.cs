// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Media.U3d;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Checks the bounded reader of U3D structure: nodes, shapes and resources are listed, and damaged or hostile files stay within limits.</summary>
public sealed class U3dReaderTests
{
    /// <summary>The faces of the test box.</summary>
    private const uint BoxFaces = 12;

    /// <summary>The positions of the test box.</summary>
    private const uint BoxPositions = 8;

    /// <summary>The normals of the test box.</summary>
    private const uint BoxNormals = 6;

    /// <summary>The nodes of the test scene: a root group, a model, a light and a view.</summary>
    private const int SceneNodes = 4;

    /// <summary>The blocks of the test scene that hold data which is kept but not decoded.</summary>
    private const int SceneDataBlocks = 2;

    /// <summary>The problems the extension test file has: one other file, one unknown block type.</summary>
    private const int ExtensionProblems = 2;

    /// <summary>The nodes of the scene that is too large for the small-scene check.</summary>
    private const int LargeScene = 100;

    /// <summary>The seed for the corruption test, so it repeats.</summary>
    private const uint Seed = 20_261_020;

    /// <summary>The multiplier of the repeatable number generator.</summary>
    private const uint Multiplier = 1_664_525;

    /// <summary>The increment of the repeatable number generator.</summary>
    private const uint Increment = 1_013_904_223;

    /// <summary>The profile flag for the extensible profile.</summary>
    private const uint Extensible = 2;

    /// <summary>The number of corrupted copies tried.</summary>
    private const int Corruptions = 400;

    /// <summary>The most bytes changed in one corrupted copy.</summary>
    private const int MostChanges = 6;

    /// <summary>The values a byte can hold.</summary>
    private const int ByteValues = 256;

    /// <summary>The bytes cut off the end of the scene.</summary>
    private const int CutBytes = 20;

    /// <summary>A modifier count or parent count no block can hold.</summary>
    private const uint HugeCount = 4_000_000_000;

    /// <summary>The blocks written to pass the block limit.</summary>
    private const int ManyBlocks = 200_100;

    /// <summary>A word of data for blocks that are kept but not decoded.</summary>
    private const uint Filler = 7;

    /// <summary>The name of the box's mesh.</summary>
    private const string BoxMesh = "BoxMesh";

    /// <summary>The name of the extension in the extension test.</summary>
    private const string FreeForm = "Free-form surfaces";

    /// <summary>The nodes, shape and resources of a scene are listed, with plain words that describe them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsTheSceneAndDescribesIt()
    {
        var scene = U3dReader.Read(Scene());
        var expected = new U3dGeometry(BoxMesh, U3dBlockType.ClodMeshDeclaration, BoxFaces, BoxPositions, BoxNormals, 0, 0);

        await Assert.That(scene.IsComplete).IsTrue();
        await Assert.That(scene.Nodes.Length).IsEqualTo(SceneNodes);
        await Assert.That(scene.CountNodes(U3dNodeKind.View)).IsEqualTo(1);
        await Assert.That(scene.Nodes.Single(static node => node.Kind == U3dNodeKind.Model).Resource).IsEqualTo(BoxMesh);
        await Assert.That(scene.Nodes.Single(static node => node.Name == "Box").Parents).IsEquivalentTo(["Root"]);
        await Assert.That(scene.Geometry.Single()).IsEqualTo(expected);
        await Assert.That(scene.CountResources(U3dBlockType.MaterialResource)).IsEqualTo(1);
        await Assert.That(scene.CountResources(U3dBlockType.TextureDeclaration)).IsEqualTo(1);
        await Assert.That(scene.ContinuationBlocks).IsEqualTo(SceneDataBlocks);
        await Assert.That(U3dSummary.Describe(scene)).IsEqualTo(
            "3D model with 1 shape, 4 scene nodes, 1 view, 1 material and 1 texture. The shapes are not drawn; the picture shown is the one the document supplies.");
    }

    /// <summary>The units factor is read only when the header says the file has units.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsDefinedUnits()
    {
        var plain = U3dReader.Read(new U3dBuilder().ToArray());
        var metres = U3dReader.Read(new U3dBuilder { Profile = U3dBuilder.DefinedUnits }.ToArray());

        await Assert.That(plain.UnitsScalingFactor).IsNull();
        await Assert.That(metres.UnitsScalingFactor).IsEqualTo(U3dBuilder.Millimetres);
        await Assert.That(metres.IsComplete).IsTrue();
    }

    /// <summary>Bytes that do not start with a file header are refused with a plain reason.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesFilesWithoutAHeader()
    {
        var scene = U3dReader.Read("This is not a model."u8);

        await Assert.That(scene.IsComplete).IsFalse();
        await Assert.That(scene.Problems.Single()).Contains("not a U3D file");
        await Assert.That(scene.Nodes).IsEmpty();
    }

    /// <summary>A file cut in the middle keeps the blocks before the cut and says it was cut.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsWhatCameBeforeATruncation()
    {
        var whole = Scene();
        var cut = whole.AsSpan(0, whole.Length - CutBytes).ToArray();

        var scene = U3dReader.Read(cut);

        await Assert.That(scene.IsComplete).IsFalse();
        await Assert.That(scene.Problems.Length).IsGreaterThan(0);
        await Assert.That(scene.Nodes.Length).IsGreaterThan(0);
    }

    /// <summary>Counts that cannot fit in their blocks or the file are reported, never trusted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsExcessiveCounts()
    {
        var manyParents = new U3dBuilder().Add(U3dBuilder.Block((uint)U3dBlockType.GroupNode, U3dBuilder.Join(U3dBuilder.Text("Group"), U3dBuilder.Words(HugeCount)))).ToArray();
        var manyModifiers = new U3dBuilder().Add(U3dBuilder.Chain("Chain", 0, HugeCount)).ToArray();
        var builder = new U3dBuilder();
        for (var i = 0; i < ManyBlocks; i++)
        {
            _ = builder.Add(U3dBuilder.Block((uint)U3dBlockType.PriorityUpdate, U3dBuilder.Words(1)));
        }

        var tooMany = U3dReader.Read(builder.ToArray());

        await Assert.That(U3dReader.Read(manyParents).Problems.Single()).Contains("more parents");
        await Assert.That(U3dReader.Read(manyModifiers).Problems.Single()).Contains("more modifiers");
        await Assert.That(tooMany.BlockCount).IsLessThanOrEqualTo(U3dReader.MaxBlocks);
        await Assert.That(tooMany.Problems.Single()).Contains("more blocks");
    }

    /// <summary>Nodes that are their own ancestors are reported, and a tree is not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsLoopsInTheNodeTree()
    {
        var loop = new U3dBuilder()
            .Add(U3dBuilder.Block((uint)U3dBlockType.GroupNode, U3dBuilder.NodeData("A", ["B"], [])))
            .Add(U3dBuilder.Block((uint)U3dBlockType.GroupNode, U3dBuilder.NodeData("B", ["C"], [])))
            .Add(U3dBuilder.Block((uint)U3dBlockType.GroupNode, U3dBuilder.NodeData("C", ["A"], [])))
            .ToArray();

        await Assert.That(U3dReader.Read(loop).Problems.Single()).Contains("loop");
        await Assert.That(U3dReader.Read(Scene()).IsComplete).IsTrue();
    }

    /// <summary>A scene larger than the small-scene path is checked the same way: a long chain is a tree, and closing it into a ring is a loop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsLoopsInALargeScene()
    {
        var chain = new U3dBuilder();
        var ring = new U3dBuilder();
        for (var i = 0; i < LargeScene; i++)
        {
            var parents = i == 0 ? Array.Empty<string>() : new[] { $"N{i - 1}" };
            _ = chain.Add(U3dBuilder.Block((uint)U3dBlockType.GroupNode, U3dBuilder.NodeData($"N{i}", parents, [])));
            _ = ring.Add(U3dBuilder.Block((uint)U3dBlockType.GroupNode, U3dBuilder.NodeData($"N{i}", [$"N{(i + LargeScene - 1) % LargeScene}"], [])));
        }

        await Assert.That(U3dReader.Read(chain.ToArray()).IsComplete).IsTrue();
        await Assert.That(U3dReader.Read(ring.ToArray()).Problems.Single()).Contains("loop");
    }

    /// <summary>Extensions, other files and block types the file does not define are noted and never followed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesExtensionsAndExternalReferences()
    {
        var file = new U3dBuilder { Profile = Extensible }
            .Add(U3dBuilder.Block((uint)U3dBlockType.NewObjectType, U3dBuilder.Text(FreeForm)))
            .Add(U3dBuilder.Block((uint)U3dBlockType.FileReference, U3dBuilder.Text("Other")))
            .Add(U3dBuilder.Block(0xFFFFFF99, U3dBuilder.Words(Filler)))
            .ToArray();

        var scene = U3dReader.Read(file);

        await Assert.That(scene.Extensions).IsEquivalentTo([FreeForm]);
        await Assert.That(scene.Problems.Length).IsEqualTo(ExtensionProblems);
        await Assert.That(scene.Problems[0]).Contains("never loaded");
        await Assert.That(U3dSummary.Describe(scene)).Contains(FreeForm);
    }

    /// <summary>Every prefix of a file and many corrupted copies are read without an exception.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverThrowsOnDamagedFiles()
    {
        var whole = Scene();
        var state = Seed;
        var failures = 0;
        for (var length = 0; length <= whole.Length; length++)
        {
            failures += Reads(whole.AsSpan(0, length).ToArray()) ? 0 : 1;
        }

        for (var attempt = 0; attempt < Corruptions; attempt++)
        {
            var copy = (byte[])whole.Clone();
            for (var flips = 0; flips <= attempt % MostChanges; flips++)
            {
                state = Step(state);
                var position = (int)(state % (uint)copy.Length);
                state = Step(state);
                copy[position] = (byte)(state % ByteValues);
            }

            failures += Reads(copy) ? 0 : 1;
        }

        await Assert.That(failures).IsEqualTo(0);
    }

    /// <summary>Builds the test scene: a group, a box model with its mesh, a light and a view, with a material and a texture.</summary>
    /// <returns>The file's bytes.</returns>
    private static byte[] Scene() => U3dBuilder.SampleModel();

    /// <summary>Moves a simple repeatable number generator on one step.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The next state.</returns>
    private static uint Step(uint state) => unchecked((state * Multiplier) + Increment);

    /// <summary>Reads bytes and tells whether the reader returned without throwing.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns><see langword="true"/> when no exception was thrown.</returns>
    private static bool Reads(byte[] data)
    {
        try
        {
            _ = U3dReader.Read(data);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }
}
