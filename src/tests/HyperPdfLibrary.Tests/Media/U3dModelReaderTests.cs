// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Media.U3d;
using static HyperPdfLibrary.Tests.Media.U3dTestScenes;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Checks that a U3D file is decoded into nodes, meshes, materials, shaders, lights and views, in the compressed and plain forms.</summary>
public sealed class U3dModelReaderTests
{
    /// <summary>The nodes of the red quad scene: the quad, the light and the camera.</summary>
    private const int RedQuadNodes = 3;

    /// <summary>The faces of the quad.</summary>
    private const int QuadFaces = 2;

    /// <summary>The cut that makes a file end inside its last block.</summary>
    private const int CutBytes = 9;

    /// <summary>The final resolution of a mesh that declares updates.</summary>
    private const uint UpdatedResolution = 40;

    /// <summary>The number of diffuse colours in the coloured mesh.</summary>
    private const int ColorCount = 3;

    /// <summary>The number of texture coordinates in the coloured mesh.</summary>
    private const int CoordinateCount = 3;

    /// <summary>A count no block can hold.</summary>
    private const uint HugeCount = 4_000_000_000;

    /// <summary>The block type of a mesh declaration.</summary>
    private const uint MeshDeclarationType = 0xFFFFFF31;

    /// <summary>The block type of a base mesh continuation.</summary>
    private const uint BaseMeshType = 0xFFFFFF3B;

    /// <summary>The block type of a group node.</summary>
    private const uint GroupType = 0xFFFFFF21;

    /// <summary>The block type of a light resource.</summary>
    private const uint LightType = 0xFFFFFF51;

    /// <summary>The block type of a view resource.</summary>
    private const uint ViewResourceType = 0xFFFFFF52;

    /// <summary>The block type of a lit texture shader.</summary>
    private const uint ShaderType = 0xFFFFFF53;

    /// <summary>The block type of a shading modifier.</summary>
    private const uint ShadingType = 0xFFFFFF45;

    /// <summary>The block type of a point set declaration.</summary>
    private const uint PointSetType = 0xFFFFFF36;

    /// <summary>The block type of a motion resource.</summary>
    private const uint MotionType = 0xFFFFFF56;

    /// <summary>The block type of an animation modifier.</summary>
    private const uint AnimationType = 0xFFFFFF43;

    /// <summary>A block type no standard defines.</summary>
    private const uint UnknownType = 0xFFFFFF99;

    /// <summary>The largest light kind the format defines plus one.</summary>
    private const byte BadLightKind = 9;

    /// <summary>The number of passes a multi-pass view resource has.</summary>
    private const uint ThreePasses = 3;

    /// <summary>The index and count the tests use where they need a small number beyond one.</summary>
    private const int ShaderLayers = 2;

    /// <summary>The alignment of the fields of a chain block.</summary>
    private const int WordBytes = 4;

    /// <summary>The chain attributes for a bounding sphere and a bounding box.</summary>
    private const uint SphereAndBox = 3;

    /// <summary>The floats of a bounding sphere and a bounding box: four and six.</summary>
    private const int BoundsFloats = 10;

    /// <summary>The scene's nodes, mesh, material, shader, light, view resource and shading are decoded, and the camera keeps its transform and settings.</summary>
    /// <param name="compressed">Whether the file is written as an arithmetic code.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task DecodesTheScene(bool compressed)
    {
        var model = U3dModelReader.Read(RedQuad(compressed));

        await Assert.That(model.IsComplete).IsTrue();
        await Assert.That(model.Nodes.Length).IsEqualTo(RedQuadNodes);
        await Assert.That(model.Meshes.Single().Faces.Length).IsEqualTo(QuadFaces);
        Vector3 corner = new(-QuadHalf, 0, -QuadHalf);
        Vector3 toCamera = new(0, -1, 0);
        Vector3 red = new(1, 0, 0);
        await Assert.That(model.Meshes.Single().Positions[0]).IsEqualTo(corner);
        await Assert.That(model.Meshes.Single().Normals.Single()).IsEqualTo(toCamera);
        await Assert.That(model.Meshes.Single().IsComplete).IsTrue();
        await Assert.That(model.Materials.Single().Ambient).IsEqualTo(red);
        await Assert.That(model.Materials.Single().UsesDiffuse).IsTrue();
        await Assert.That(model.Materials.Single().UsesOpacity).IsTrue();
        await Assert.That(model.Shaders.Single().Material).IsEqualTo(RedMaterial);
        await Assert.That(model.Shaders.Single().IsLit).IsTrue();
        await Assert.That(model.Shaders.Single().UsesVertexColor).IsFalse();
        await Assert.That(model.Lights.Single().IsEnabled).IsTrue();
        await Assert.That(model.Lights.Single().Kind).IsEqualTo(U3dLightKind.Ambient);
        await Assert.That(model.ViewResources.Single().Name).IsEqualTo(ViewResource);
        await Assert.That(model.Shadings.Single().Lists.Single().Single()).IsEqualTo(RedShader);
        await Assert.That(model.ViewNodes().Single().Name).IsEqualTo(CameraNode);
    }

    /// <summary>The camera node's transform, projection and view port are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsTheViewNode()
    {
        var camera = U3dModelReader.Read(RedQuad(true)).ViewNodes().Single();

        Vector3 place = new(0, -CameraDistance, 0);
        await Assert.That(camera.Transforms.Single().Translation).IsEqualTo(place);
        await Assert.That(camera.View!.FieldOfView).IsEqualTo(FieldOfView);
        await Assert.That(camera.View.NearClip).IsEqualTo(Near);
        await Assert.That(camera.View.FarClip).IsEqualTo(Far);
        await Assert.That(camera.View.IsOrthographic).IsFalse();
        await Assert.That(camera.View.BackdropCount).IsEqualTo(1);
        await Assert.That(camera.View.OverlayCount).IsEqualTo(0);
        await Assert.That(camera.Resource).IsEqualTo(ViewResource);
    }

    /// <summary>Orthographic and one-point views are told apart from perspective views.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsOtherProjections()
    {
        var writer = new U3dFileWriter(false);
        var file = writer
            .Add(writer.OrthographicView("Ortho", ViewResource, Near, Far, CameraDistance, Root))
            .Add(writer.OnePointView("OnePoint", ViewResource, Root))
            .ToArray();

        var views = U3dModelReader.Read(file).ViewNodes();

        await Assert.That(views[0].View!.IsOrthographic).IsTrue();
        await Assert.That(views[0].View!.OrthographicHeight).IsEqualTo(CameraDistance);
        await Assert.That(views[1].View!.IsOrthographic).IsFalse();
        await Assert.That(views[1].View!.FieldOfView).IsEqualTo(0F);
    }

    /// <summary>A mesh with colours, texture coordinates and no normals keeps each corner's indices.</summary>
    /// <param name="compressed">Whether the file is written as an arithmetic code.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task DecodesColoursAndCoordinates(bool compressed)
    {
        var writer = new U3dFileWriter(compressed);
        var mesh = new U3dMeshSpec(
            "Tri",
            [new(0, 0, 0), new(1, 0, 0), new(0, 0, 1)],
            [],
            [new(1, 0, 0, 1), new(0, 1, 0, 1), new(0, 0, 1, 1)],
            [new(0, 0, 0, 0), new(1, 0, 0, 0), new(0, 1, 0, 0)],
            [new(0, [0, 1, ShaderLayers], [], [0, 1, ShaderLayers], [0, 1, ShaderLayers])],
            [new(true, [ShaderLayers])],
            ColorCount);

        var model = U3dModelReader.Read(writer.Add(writer.Chain("Tri", 1, writer.MeshDeclaration(mesh, true))).Add(writer.BaseMesh(mesh, true)).ToArray());

        var face = model.Meshes.Single().Faces.Single();
        await Assert.That(model.IsComplete).IsTrue();
        await Assert.That(model.Meshes.Single().DiffuseColors.Length).IsEqualTo(ColorCount);
        await Assert.That(model.Meshes.Single().TextureCoordinates.Length).IsEqualTo(CoordinateCount);
        U3dCorner last = new(ShaderLayers, -1, ShaderLayers, ShaderLayers);
        await Assert.That(face.C).IsEqualTo(last);
    }

    /// <summary>A mesh that declares resolution updates is its base mesh, and the file says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesResolutionUpdates()
    {
        var writer = new U3dFileWriter(true);
        var quad = Quad(QuadResource, UpdatedResolution);

        var model = U3dModelReader.Read(writer.Add(writer.Chain(QuadResource, 1, writer.MeshDeclaration(quad, false))).Add(writer.BaseMesh(quad, false)).ToArray());

        await Assert.That(model.Meshes.Single().IsComplete).IsFalse();
        await Assert.That(model.IsComplete).IsFalse();
        await Assert.That(model.Problems.Single()).Contains("resolution updates");
    }

    /// <summary>A mesh with no base mesh is reported and gives nothing to draw.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesAMeshWithoutABaseMesh()
    {
        var writer = new U3dFileWriter(true);
        var empty = new U3dMeshSpec("Empty", [], [], [], [], [], [new(false, [])], 0);

        var model = U3dModelReader.Read(writer.Add(writer.Chain("Empty", 1, writer.MeshDeclaration(empty, false))).ToArray());

        await Assert.That(model.Meshes).IsEmpty();
        await Assert.That(model.Problems.Single()).Contains("no base mesh");
    }

    /// <summary>A file that is not U3D gives an empty model with the reason.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesFilesWithoutAHeader()
    {
        var model = U3dModelReader.Read("This is not a model."u8.ToArray());

        await Assert.That(model.Nodes).IsEmpty();
        await Assert.That(model.Problems.Single()).Contains("not a U3D file");
    }

    /// <summary>A null set of limits is refused, and a cancelled token stops the decode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksItsArguments()
    {
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();
        var file = RedQuad(true);

        await Assert.That(() => U3dModelReader.Read(file, null!, CancellationToken.None)).Throws<ArgumentNullException>();
        await Assert.That(() => U3dModelReader.Read(file, U3dLimits.Default, cancel.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Counts no block can hold are reported, and nothing is allocated for them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsExcessiveCounts()
    {
        var huge = new U3dValueWriter(true).String("Huge").U32(0).U32(0).U32(HugeCount).U32(HugeCount).ToArray();
        var parents = new U3dValueWriter(true).String("Node").U32(HugeCount).ToArray();
        var lists = new U3dValueWriter(true).String("S").U32(0).U32(0).U32(0).U32(0).U32(0).U32(0).U32(0).U32(0).U32(1).U32(0).U32(HugeCount).ToArray();
        var shadings = new U3dValueWriter(true).String("S").U32(0).U32(0).U32(0).U32(0).U32(0).U32(0).U32(0).U32(0).U32(HugeCount).ToArray();
        var passes = new U3dValueWriter(true).String("V").U32(HugeCount).ToArray();
        var shaderLists = new U3dValueWriter(true).String("S").U32(0).U32(0).U32(HugeCount).ToArray();
        var writer = new U3dFileWriter(false);

        var model = U3dModelReader.Read(writer
            .Add(U3dFileWriter.Block(MeshDeclarationType, huge))
            .Add(U3dFileWriter.Block(GroupType, parents))
            .Add(U3dFileWriter.Block(ViewResourceType, passes))
            .Add(U3dFileWriter.Block(ShadingType, shaderLists))
            .Add(U3dFileWriter.Block(MeshDeclarationType, lists))
            .Add(U3dFileWriter.Block(MeshDeclarationType, shadings))
            .ToArray());

        await Assert.That(model.Meshes).IsEmpty();
        await Assert.That(model.Nodes).IsEmpty();
        await Assert.That(model.ViewResources).IsEmpty();
        await Assert.That(model.Shadings).IsEmpty();
        await Assert.That(model.Problems.Length).IsGreaterThanOrEqualTo(ShaderLayers);
    }

    /// <summary>The limits on faces, positions, nodes and blocks stop a decode that would be too large.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StaysWithinTheLimits()
    {
        var writer = new U3dFileWriter(true);
        var quad = Quad(QuadResource, QuadCorners);
        var file = writer
            .Add(writer.Chain(QuadResource, 1, writer.MeshDeclaration(quad, false)))
            .Add(writer.BaseMesh(quad, false))
            .Add(writer.Group("A", Root))
            .Add(writer.Group("B", Root))
            .ToArray();
        var tight = new U3dLimits(1, 1, 1, 1, 1, 1, 1, 1);

        var limited = U3dModelReader.Read(file, tight, CancellationToken.None);

        await Assert.That(limited.Meshes).IsEmpty();
        await Assert.That(limited.Nodes.Length).IsLessThanOrEqualTo(1);
        await Assert.That(limited.Problems.Length).IsGreaterThan(0);
    }

    /// <summary>A node limit is reported once and the later nodes are dropped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StopsAtTheNodeLimit()
    {
        var writer = new U3dFileWriter(true);
        var file = writer.Add(writer.Group("A", Root)).Add(writer.Group("B", Root)).Add(writer.Group("C", Root)).ToArray();
        var limits = U3dLimits.Default with { MaxNodes = 1 };

        var model = U3dModelReader.Read(file, limits, CancellationToken.None);

        await Assert.That(model.Nodes.Length).IsEqualTo(1);
        await Assert.That(model.Problems.Single()).Contains("more scene nodes");
    }

    /// <summary>A base mesh that is bigger than its declaration, cut short, or names a missing mesh or shader list is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesDamagedBaseMeshes()
    {
        var writer = new U3dFileWriter(false);
        var quad = Quad(QuadResource, QuadCorners);
        var declaration = writer.Chain(QuadResource, 1, writer.MeshDeclaration(quad, false));
        var unknown = U3dFileWriter.Block(BaseMeshType, new U3dValueWriter(true).String("Nobody").U32(0).ToArray());
        var larger = U3dFileWriter.Block(BaseMeshType, new U3dValueWriter(true).String(QuadResource).U32(0).U32(HugeCount).U32(1).U32(1).U32(1).U32(1).U32(1).ToArray());
        var fits = U3dFileWriter.Block(BaseMeshType, new U3dValueWriter(true).String(QuadResource).U32(0).U32(QuadFaces).U32(QuadCorners).U32(1).U32(0).U32(0).U32(0).ToArray());
        var badShading = U3dFileWriter.Block(BaseMeshType, BaseMeshWithFace(quad, ShaderLayers, 0));
        var badIndex = U3dFileWriter.Block(BaseMeshType, BaseMeshWithFace(quad, 0, HugeCount));
        var cut = U3dFileWriter.Block(BaseMeshType, new U3dValueWriter(true).String(QuadResource).U32(0).ToArray());

        var model = U3dModelReader.Read(new U3dFileWriter(false).Add(declaration).Add(unknown).Add(larger).Add(fits).Add(badShading).Add(badIndex).Add(cut).ToArray());

        await Assert.That(model.Meshes).IsEmpty();
        await Assert.That(Mentions(model, "does not declare")).IsTrue();
        await Assert.That(Mentions(model, "declares more values")).IsTrue();
        await Assert.That(Mentions(model, "cut short or holds a face")).IsTrue();
    }

    /// <summary>Blocks the decoder does not draw or play, and extensions it does not read, are noted with plain reasons.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesWhatIsNotDrawn()
    {
        var writer = new U3dFileWriter(false);
        var file = writer
            .Add(U3dFileWriter.Block(PointSetType, new U3dValueWriter(true).String("Points").ToArray()))
            .Add(U3dFileWriter.Block(MotionType, new U3dValueWriter(true).String("Move").ToArray()))
            .Add(U3dFileWriter.Block(AnimationType, new U3dValueWriter(true).String("Anim").ToArray()))
            .Add(U3dFileWriter.Block(UnknownType, new U3dValueWriter(true).U32(1).ToArray()))
            .ToArray();

        var model = U3dModelReader.Read(file);

        await Assert.That(Mentions(model, "Point sets")).IsTrue();
        await Assert.That(Mentions(model, "Animations")).IsTrue();
        await Assert.That(Mentions(model, "Modifiers other than shading")).IsTrue();
        await Assert.That(Mentions(model, "does not know")).IsTrue();
    }

    /// <summary>A light that names a kind the format does not define, a cut-short resource and a cut-short node are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesDamagedResources()
    {
        var writer = new U3dFileWriter(false);
        var badLight = new U3dValueWriter(true).String("L").U32(1).U8(BadLightKind).Vector3(Vector3.One).F32(1).Vector3(Vector3.One).F32(0).F32(1).ToArray();
        var file = writer
            .Add(U3dFileWriter.Block(LightType, badLight))
            .Add(U3dFileWriter.Block(LightType, new U3dValueWriter(true).String("Cut").ToArray()))
            .Add(U3dFileWriter.Block(ShaderType, new U3dValueWriter(true).String("Cut").ToArray()))
            .Add(U3dFileWriter.Block(GroupType, new U3dValueWriter(true).String("Cut").ToArray()))
            .Add(U3dFileWriter.Block(0xFFFFFF54, new U3dValueWriter(true).String("Cut").ToArray()))
            .ToArray();

        var model = U3dModelReader.Read(file);

        await Assert.That(model.Lights).IsEmpty();
        await Assert.That(model.Shaders).IsEmpty();
        await Assert.That(model.Materials).IsEmpty();
        await Assert.That(model.Nodes).IsEmpty();
        await Assert.That(model.Problems.Length).IsGreaterThan(0);
    }

    /// <summary>A view resource with several passes keeps its first pass, and a shader keeps the textures of its active layers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsPassesAndShaderLayers()
    {
        var writer = new U3dFileWriter(false);
        var file = writer
            .Add(writer.ViewResource("Multi", ThreePasses))
            .Add(writer.Shader("Layered", 1, RedMaterial, "A", "B"))
            .ToArray();

        var model = U3dModelReader.Read(file);

        await Assert.That(model.ViewResources.Single().FogEnabled).IsTrue();
        await Assert.That(model.ViewResources.Single().RootNode).IsEmpty();
        await Assert.That(model.Shaders.Single().Textures).IsEquivalentTo(["A", "B"]);
    }

    /// <summary>A modifier chain that is cut short, or declares more modifiers than the limit, is noted and the blocks around it still decode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesDamagedChains()
    {
        var writer = new U3dFileWriter(false);
        var huge = U3dFileWriter.Block(0xFFFFFF14, new U3dValueWriter(true).String("Chain").U32(0).U32(0).U32(HugeCount).ToArray());
        var cut = U3dFileWriter.Block(0xFFFFFF14, new U3dValueWriter(true).String("Chain").ToArray());
        var file = writer.Add(huge).Add(cut).Add(writer.Group("After", Root)).ToArray();

        var model = U3dModelReader.Read(file);

        await Assert.That(model.Nodes.Single().Name).IsEqualTo("After");
        await Assert.That(Mentions(model, "modifier chain")).IsTrue();
    }

    /// <summary>A file cut short inside its last block keeps what came before it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsWhatCameBeforeATruncation()
    {
        var whole = RedQuad(true);

        var model = U3dModelReader.Read(whole.AsMemory(0, whole.Length - CutBytes));

        await Assert.That(model.Meshes).IsNotEmpty();
        await Assert.That(model.IsComplete).IsFalse();
    }

    /// <summary>A bounding sphere and box on a chain are skipped before its modifiers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipsChainBounds()
    {
        var writer = new U3dFileWriter(false);
        var inner = writer.Group("Inside", Root);
        var data = new U3dValueWriter(true).String("Bounded").U32(0).U32(SphereAndBox);
        for (var i = 0; i < BoundsFloats; i++)
        {
            _ = data.F32(0);
        }

        var head = data.ToArray();
        var padding = new byte[(WordBytes - (head.Length % WordBytes)) % WordBytes];
        var chain = U3dFileWriter.Block(0xFFFFFF14, [.. head, .. padding, .. new U3dValueWriter(true).U32(1).ToArray(), .. inner]);

        var model = U3dModelReader.Read(writer.Add(chain).ToArray());

        await Assert.That(model.Nodes.Single().Name).IsEqualTo("Inside");
    }

    /// <summary>Tells whether a model's problems mention some text.</summary>
    /// <param name="model">The model.</param>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool Mentions(U3dModel model, string text) => Array.Exists(model.Problems, p => p.Contains(text, StringComparison.Ordinal));

    /// <summary>Builds a base mesh block's data holding one face with the given shading and position index, for the damaged-mesh test.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="shading">The shading index to write.</param>
    /// <param name="position">The first corner's position index to write.</param>
    /// <returns>The data.</returns>
    private static byte[] BaseMeshWithFace(U3dMeshSpec mesh, uint shading, uint position)
    {
        var data = new U3dValueWriter(true).String(mesh.Name).U32(0).U32(1).U32(1).U32(1).U32(0).U32(0).U32(0);
        _ = data.Vector3(Vector3.Zero).Vector3(Vector3.UnitY).U32(shading).U32(position).U32(0);
        _ = data.U32(0).U32(0).U32(0).U32(0).U32(0);
        return data.ToArray();
    }
}
