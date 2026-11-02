// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Media.U3d;
using static HyperPdfLibrary.Tests.Media.U3dFramePixels;
using static HyperPdfLibrary.Tests.Media.U3dTestScenes;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Checks the frames the software rasteriser draws of small scenes whose pictures can be worked out by hand.</summary>
public sealed class U3dRendererTests
{
    /// <summary>The width and height of the test frames.</summary>
    private const int Side = 64;

    /// <summary>The centre pixel of a test frame.</summary>
    private const int Centre = 32;

    /// <summary>The pixel offset that is inside one quarter of the quad in a test frame.</summary>
    private const int InsideQuad = 8;

    /// <summary>The bytes of a pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The fewest red pixels the quad covers: it spans about 22 pixels each way.</summary>
    private const int LeastQuadPixels = 440;

    /// <summary>The most red pixels the quad covers.</summary>
    private const int MostQuadPixels = 540;

    /// <summary>The red pixels an orthographic view of height four gives: the quad fills a quarter of the frame.</summary>
    private const int QuarterFrame = Side * Side / PixelBytes;

    /// <summary>The angle that tilts a directional light so its light strikes the quad at half strength.</summary>
    private const float HalfStrengthTilt = 60;

    /// <summary>The degrees in a half turn.</summary>
    private const float HalfTurn = 180;

    /// <summary>The red channel the half-strength light gives.</summary>
    private const uint HalfRed = 128;

    /// <summary>The room allowed for rounding.</summary>
    private const uint Rounding = 2;

    /// <summary>The height of the orthographic view: the quad fills half of it.</summary>
    private const float OrthographicHeight = 4;

    /// <summary>The distance the second parent shifts a node.</summary>
    private const float Shift = 2;

    /// <summary>The distance a rig group shifts the quad.</summary>
    private const float RigShift = 1.5F;

    /// <summary>The offset in pixels that reaches the quad the rig shifts.</summary>
    private const int ShiftedPixels = 16;

    /// <summary>The distance behind the camera the floor reaches.</summary>
    private const float Behind = -8;

    /// <summary>The distance in front of the camera the floor reaches.</summary>
    private const float Ahead = 5;

    /// <summary>The height of the floor below the camera's eye.</summary>
    private const float FloorHeight = -1;

    /// <summary>The position of the point light on the camera's side of the quad.</summary>
    private const float PointDistance = -3;

    /// <summary>The number of triangles in the red quad.</summary>
    private const int QuadTriangles = 2;

    /// <summary>The side of the texture in pixels.</summary>
    private const uint TextureSide = 2;

    /// <summary>The red of a face with no shader of its own: the default material's 0.75 as a byte.</summary>
    private const uint DefaultGrey = 191;

    /// <summary>The opacity of the transparent material.</summary>
    private const float HalfOpacity = 0.5F;

    /// <summary>A frame side that gives more pixels than a frame may have.</summary>
    private const int TooManyPixelsSide = 5_000;

    /// <summary>The shader attributes for lighting and per-vertex colour.</summary>
    private const uint VertexColorShader = 5;

    /// <summary>The name of the texture the texture test draws.</summary>
    private const string Checker = "Checker";

    /// <summary>The PNG compression type.</summary>
    private const byte Png = 2;

    /// <summary>Every channel a continuation image can supply.</summary>
    private const byte AllChannels = 0x1F;

    /// <summary>The red quad is drawn red on the background, with the size it should have, in both forms of file.</summary>
    /// <param name="compressed">Whether the file is written as an arithmetic code.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task DrawsTheQuad(bool compressed)
    {
        var frame = Render(U3dModelReader.Read(RedQuad(compressed)));

        await Assert.That(Pixel(frame, Centre, Centre)).IsEqualTo(Red);
        await Assert.That(Pixel(frame, 1, 1)).IsEqualTo(Blue);
        await Assert.That(Count(frame, Red)).IsBetween(LeastQuadPixels, MostQuadPixels);
        await Assert.That(frame.TrianglesDrawn).IsEqualTo(QuadTriangles);
        await Assert.That(frame.ViewName).IsEqualTo(CameraNode);
        await Assert.That(frame.Rgba.Length).IsEqualTo(Side * Side * PixelBytes);
    }

    /// <summary>A node whose visibility is front only draws the quad that faces the camera, and a back-only node does not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HonoursVisibility()
    {
        var front = Render(U3dModelReader.Read(RedQuad(false, FrontOnly)));
        var back = Render(U3dModelReader.Read(RedQuad(false, BackOnly)));
        var none = Render(U3dModelReader.Read(RedQuad(false, 0)));

        await Assert.That(Pixel(front, Centre, Centre)).IsEqualTo(Red);
        await Assert.That(Pixel(back, Centre, Centre)).IsEqualTo(Blue);
        await Assert.That(Pixel(none, Centre, Centre)).IsEqualTo(Blue);
    }

    /// <summary>A quad wound the other way is a back face.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WindingDecidesTheFrontFace()
    {
        var quad = Quad(QuadResource, QuadCorners) with { Faces = [Face(CornerA, CornerC, CornerB), Face(CornerA, CornerD, CornerC)] };
        var file = new U3dFileWriter(false).WithMesh(quad).WithNode(QuadResource, FrontOnly, Root).WithAmbient().WithCamera().ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(Pixel(frame, Centre, Centre)).IsEqualTo(Blue);
    }

    /// <summary>An orthographic view draws the quad at half the frame's size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsAnOrthographicView()
    {
        var writer = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners)).WithNode(QuadResource, BothSides, Root).WithAmbient();
        var file = writer
            .Add(writer.ViewResource(ViewResource, 1))
            .Add(writer.OrthographicView(CameraNode, ViewResource, Near, Far, OrthographicHeight, CameraPlace))
            .ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(Pixel(frame, Centre, Centre)).IsEqualTo(Red);
        await Assert.That(Pixel(frame, 1, Centre)).IsEqualTo(Blue);
        await Assert.That(Count(frame, Red)).IsBetween(QuarterFrame - Side, QuarterFrame + Side);
    }

    /// <summary>A one-point view that carries no field of view uses the default one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsAOnePointView()
    {
        var writer = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners)).WithNode(QuadResource, BothSides, Root).WithAmbient();
        var file = writer.Add(writer.OnePointView(CameraNode, ViewResource, CameraPlace)).ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(Pixel(frame, Centre, Centre)).IsEqualTo(Red);
    }

    /// <summary>A model with no view is framed by a camera that sees all of it, and a name that matches no view does the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FramesAModelWithoutAView()
    {
        var file = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners)).WithNode(QuadResource, BothSides, Root).WithAmbient().ToArray();
        var withView = RedQuad(false);

        var framed = Render(U3dModelReader.Read(file));
        var missing = U3dRenderer.Render(U3dModelReader.Read(withView), Options("NoSuchView"), CancellationToken.None);

        await Assert.That(framed.ViewName).IsEmpty();
        await Assert.That(Pixel(framed, Centre, Centre)).IsEqualTo(Red);
        await Assert.That(missing.ViewName).IsEmpty();
        await Assert.That(Count(framed, Red)).IsGreaterThan(LeastQuadPixels);
    }

    /// <summary>A scene with no mesh gives a frame of the background alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsNothingWithoutMeshes()
    {
        var file = new U3dFileWriter(false).WithAmbient().WithCamera().ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(Count(frame, Blue)).IsEqualTo(Side * Side);
        await Assert.That(frame.TrianglesDrawn).IsEqualTo(0);
    }

    /// <summary>A directional light tilted so that it strikes the surface at an angle gives the cosine of that angle.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LightsWithADirectionalLight()
    {
        var writer = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners)).WithNode(QuadResource, BothSides, Root).WithCamera();
        var tilt = new U3dParent(string.Empty, Matrix4x4.CreateRotationX(HalfStrengthTilt * MathF.PI / HalfTurn));
        var file = writer
            .Add(writer.Light(SunLight, 1, Directional, new(1, 1, 1), 1))
            .Add(writer.LightNode("SunNode", SunLight, tilt))
            .ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(Pixel(frame, Centre, Centre) >> RedShift).IsBetween(HalfRed - Rounding, HalfRed + Rounding);
    }

    /// <summary>A point light lights the quad from a distance, and a light that is switched off does not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LightsWithAPointLight()
    {
        var place = new U3dParent(string.Empty, Matrix4x4.CreateTranslation(0, PointDistance, 0));
        var on = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners)).WithNode(QuadResource, BothSides, Root).WithCamera();
        var lit = on.Add(on.Light("Bulb", 1, Point, new(1, 1, 1), 1)).Add(on.LightNode("BulbNode", "Bulb", place)).ToArray();
        var dark = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners)).WithNode(QuadResource, BothSides, Root).WithCamera();
        var off = dark.Add(dark.Light("Off", 0, Point, new(1, 1, 1), 1)).Add(dark.LightNode("OffNode", "Off", place)).ToArray();

        var bright = Render(U3dModelReader.Read(lit));
        var fallback = Render(U3dModelReader.Read(off));

        await Assert.That(Pixel(bright, Centre, Centre) >> RedShift).IsGreaterThan(HalfRed);
        await Assert.That(Pixel(fallback, Centre, Centre)).IsEqualTo(Red);
    }

    /// <summary>A scene with no lights at all is lit by the fallback light, so the quad is not black.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LightsWithTheFallbackLight()
    {
        var file = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners)).WithNode(QuadResource, BothSides, Root).WithCamera().ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(Pixel(frame, Centre, Centre)).IsEqualTo(Red);
    }

    /// <summary>A material with opacity below one blends over what is behind it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BlendsTransparentMaterials()
    {
        var writer = new U3dFileWriter(false);
        var quad = Quad(QuadResource, QuadCorners);
        var file = writer
            .Add(writer.Chain(QuadResource, 1, writer.MeshDeclaration(quad, false)))
            .Add(writer.BaseMesh(quad, false))
            .Add(writer.Material(RedMaterial, new(1, 0, 0), new(1, 0, 0), HalfOpacity))
            .Add(writer.Shader(RedShader, 1, RedMaterial))
            .WithNode(QuadResource, BothSides, Root)
            .WithAmbient()
            .WithCamera()
            .ToArray();

        var frame = Render(U3dModelReader.Read(file));

        var centre = Pixel(frame, Centre, Centre);
        await Assert.That(centre >> RedShift).IsBetween(HalfRed - 1, HalfRed + 1);
        await Assert.That((centre >> BlueShift) & ChannelMask).IsBetween(HalfRed - 1, HalfRed + 1);
    }

    /// <summary>A face whose shader list the node does not bind is drawn in the default grey.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsesTheDefaultStyleForUnboundLists()
    {
        var writer = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners));
        var file = writer
            .Add(writer.Chain(QuadNode, 0, writer.ModelNode(QuadNode, QuadResource, BothSides, Root)))
            .WithAmbient()
            .WithCamera()
            .ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(Pixel(frame, Centre, Centre) >> RedShift).IsEqualTo(DefaultGrey);
    }

    /// <summary>A node with two parents is drawn in both places, and a group's transform moves its children.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsEveryPlaceOfANode()
    {
        var writer = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners));
        var shifted = new U3dParent(string.Empty, Matrix4x4.CreateTranslation(Shift, 0, 0));
        var file = writer.WithNode(QuadResource, BothSides, Root, shifted).WithAmbient().WithCamera().ToArray();
        var rigged = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners));
        var rig = new U3dParent("Rig", Matrix4x4.Identity);
        var moved = rigged
            .Add(rigged.Group("Rig", new U3dParent(string.Empty, Matrix4x4.CreateTranslation(RigShift, 0, 0))))
            .WithNode(QuadResource, BothSides, rig)
            .WithAmbient()
            .WithCamera()
            .ToArray();

        var two = Render(U3dModelReader.Read(file));
        var one = Render(U3dModelReader.Read(moved));

        await Assert.That(Pixel(two, Centre, Centre)).IsEqualTo(Red);
        await Assert.That(Pixel(two, Centre + ShiftedPixels + InsideQuad, Centre)).IsEqualTo(Red);
        await Assert.That(Pixel(one, Centre, Centre)).IsEqualTo(Blue);
        await Assert.That(Pixel(one, Centre + ShiftedPixels, Centre)).IsEqualTo(Red);
    }

    /// <summary>Nodes that are their own ancestors, and a parent the file does not define, do not hang the scene.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SurvivesLoopsAndMissingParents()
    {
        var writer = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners));
        var file = writer
            .Add(writer.Group("A", new U3dParent("B", Matrix4x4.Identity)))
            .Add(writer.Group("B", new U3dParent("A", Matrix4x4.Identity)))
            .WithNode(QuadResource, BothSides, new U3dParent("A", Matrix4x4.Identity), new U3dParent("Nowhere", Matrix4x4.Identity))
            .WithAmbient()
            .WithCamera()
            .ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(frame.TrianglesDrawn).IsGreaterThan(0);
    }

    /// <summary>A floor that reaches behind the camera is cut at the near plane, and the part in front is drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClipsAtTheNearPlane()
    {
        var floor = new U3dMeshSpec(
            QuadResource,
            [new(-1, Behind, FloorHeight), new(1, Behind, FloorHeight), new(1, Ahead, FloorHeight), new(-1, Ahead, FloorHeight)],
            [new(0, 0, 1)],
            [],
            [],
            [Face(CornerA, CornerB, CornerC), Face(CornerA, CornerC, CornerD)],
            [new(false, [])],
            QuadCorners);
        var file = new U3dFileWriter(false).WithMesh(floor).WithNode(QuadResource, BothSides, Root).WithAmbient().WithCamera().ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(Count(frame, Red)).IsGreaterThan(0);
        await Assert.That(Pixel(frame, Centre, 1)).IsEqualTo(Blue);
    }

    /// <summary>A face wholly behind the camera is not drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DropsFacesBehindTheCamera()
    {
        var behind = new U3dParent(string.Empty, Matrix4x4.CreateTranslation(0, -CameraDistance * Shift, 0));
        var file = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners)).WithNode(QuadResource, BothSides, behind).WithAmbient().WithCamera().ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(frame.TrianglesDrawn).IsEqualTo(0);
    }

    /// <summary>Per-vertex colours replace the material's when the shader uses them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsesVertexColours()
    {
        var quad = Quad(QuadResource, QuadCorners) with
        {
            DiffuseColors = [new(0, 1, 0, 1)],
            Faces = [Coloured(CornerA, CornerB, CornerC), Coloured(CornerA, CornerC, CornerD)],
            Shadings = [new(true, [])],
        };
        var writer = new U3dFileWriter(false);
        var file = writer
            .Add(writer.Chain(QuadResource, 1, writer.MeshDeclaration(quad, false)))
            .Add(writer.BaseMesh(quad, false))
            .Add(writer.Material(RedMaterial, new(1, 1, 1), new(1, 1, 1), 1))
            .Add(writer.Shader(RedShader, VertexColorShader, RedMaterial))
            .WithNode(QuadResource, BothSides, Root)
            .WithSun()
            .WithCamera()
            .ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(Pixel(frame, Centre, Centre)).IsEqualTo(Green);
    }

    /// <summary>A mesh with no normals is lit by the normals of its faces, and a zero normal does the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FallsBackToFaceNormals()
    {
        var noNormals = Quad(QuadResource, QuadCorners) with { Normals = [], Faces = [Plain(CornerA, CornerB, CornerC), Plain(CornerA, CornerC, CornerD)] };
        var zero = Quad(QuadResource, QuadCorners) with { Normals = [Vector3.Zero] };
        var fileA = new U3dFileWriter(false).WithMesh(noNormals, true).WithNode(QuadResource, BothSides, Root).WithSun().WithCamera().ToArray();
        var fileB = new U3dFileWriter(false).WithMesh(zero).WithNode(QuadResource, BothSides, Root).WithSun().WithCamera().ToArray();

        var frameA = Render(U3dModelReader.Read(fileA));
        var frameB = Render(U3dModelReader.Read(fileB));

        await Assert.That(Pixel(frameA, Centre, Centre)).IsEqualTo(Red);
        await Assert.That(Pixel(frameB, Centre, Centre)).IsEqualTo(Red);
    }

    /// <summary>A texture is drawn over the surface with the coordinates of its corners.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsATexture()
    {
        var quad = Quad(QuadResource, QuadCorners) with
        {
            TextureCoordinates = [new(0, 1, 0, 0), new(1, 1, 0, 0), new(1, 0, 0, 0), new(0, 0, 0, 0)],
            Faces = [Textured(CornerA, CornerB, CornerC), Textured(CornerA, CornerC, CornerD)],
            Shadings = [new(false, [TextureSide])],
        };
        byte[] pixels = [255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255];
        var writer = new U3dFileWriter(true);
        var file = writer
            .Add(writer.Chain(QuadResource, 1, writer.MeshDeclaration(quad, false)))
            .Add(writer.BaseMesh(quad, false))
            .Add(writer.TextureDeclaration(Checker, TextureSide, TextureSide, Png, AllChannels))
            .Add(writer.TextureContinuation(Checker, 0, U3dPng.Rgba((int)TextureSide, (int)TextureSide, pixels)))
            .Add(writer.Material(RedMaterial, new(1, 1, 1), new(1, 1, 1), 1))
            .Add(writer.Shader(RedShader, 1, RedMaterial, Checker))
            .WithNode(QuadResource, BothSides, Root)
            .WithAmbient()
            .WithCamera()
            .ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(Pixel(frame, Centre - InsideQuad, Centre - InsideQuad)).IsEqualTo(Red);
        await Assert.That(Pixel(frame, Centre + InsideQuad, Centre - InsideQuad)).IsEqualTo(Green);
        await Assert.That(Pixel(frame, Centre - InsideQuad, Centre + InsideQuad)).IsEqualTo(Blue);
        await Assert.That(Pixel(frame, Centre + InsideQuad, Centre + InsideQuad)).IsEqualTo(White);
    }

    /// <summary>A back face is lit on the side the viewer sees, so a quad whose normals point away is not black.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LightsTheSideTheViewerSees()
    {
        var away = Quad(QuadResource, QuadCorners) with { Normals = [new(0, 1, 0)] };
        var file = new U3dFileWriter(false).WithMesh(away).WithNode(QuadResource, BothSides, Root).WithCamera().ToArray();

        var frame = Render(U3dModelReader.Read(file));

        await Assert.That(Pixel(frame, Centre, Centre)).IsEqualTo(Red);
    }

    /// <summary>Sizes that are not positive, too large, or missing options are refused, and a cancelled token stops the drawing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksItsArguments()
    {
        var model = U3dModelReader.Read(RedQuad(false));
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();

        await Assert.That(static () => U3dRenderer.Render(null!, Options(null), CancellationToken.None)).Throws<ArgumentNullException>();
        await Assert.That(() => U3dRenderer.Render(model, null!, CancellationToken.None)).Throws<ArgumentNullException>();
        await Assert.That(() => U3dRenderer.Render(model, new(0, Side, null, Vector4.Zero), CancellationToken.None)).Throws<ArgumentException>();
        await Assert.That(() => U3dRenderer.Render(model, new(Side, -1, null, Vector4.Zero), CancellationToken.None)).Throws<ArgumentException>();
        await Assert.That(() => U3dRenderer.Render(model, new(TooManyPixelsSide, TooManyPixelsSide, null, Vector4.Zero), CancellationToken.None)).Throws<ArgumentException>();
        await Assert.That(() => U3dRenderer.Render(model, Options(null), cancel.Token)).Throws<OperationCanceledException>();
        await Assert.That(U3dRenderOptions.MaxPixels).IsGreaterThan(Side * Side);
    }

    /// <summary>Collecting the triangles of a frame gives them far to near, drops the ones that are culled, and checks its arguments.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CollectsTrianglesFarToNear()
    {
        var writer = new U3dFileWriter(false).WithMesh(Quad(QuadResource, QuadCorners));
        var farther = new U3dParent(string.Empty, Matrix4x4.CreateTranslation(0, Shift, 0));
        var file = writer.WithNode(QuadResource, BothSides, Root, farther).WithAmbient().WithCamera().ToArray();
        var model = U3dModelReader.Read(file);

        var list = U3dRenderer.Collect(model, Options(null), CancellationToken.None);
        var culled = U3dRenderer.Collect(U3dModelReader.Read(RedQuad(false, BackOnly)), Options(null), CancellationToken.None);

        await Assert.That(list.Triangles.Length).IsEqualTo(QuadTriangles * QuadTriangles);
        await Assert.That(list.Triangles[0].Depth).IsGreaterThan(list.Triangles[^1].Depth);
        await Assert.That(list.Width).IsEqualTo(Side);
        await Assert.That(list.ViewName).IsEqualTo(CameraNode);
        await Assert.That(list.Triangles[0].Texture).IsNull();
        await Assert.That(culled.Triangles).IsEmpty();
        await Assert.That(() => U3dRenderer.Collect(model, new(0, Side, null, Vector4.Zero), CancellationToken.None)).Throws<ArgumentException>();
    }

    /// <summary>Draws a model in the standard frame.</summary>
    /// <param name="model">The model.</param>
    /// <returns>The frame.</returns>
    private static U3dFrame Render(U3dModel model) => U3dRenderer.Render(model, Options(null), CancellationToken.None);

    /// <summary>Makes the standard options: a blue background.</summary>
    /// <param name="view">The view name, or <see langword="null"/>.</param>
    /// <returns>The options.</returns>
    private static U3dRenderOptions Options(string? view) => new(Side, Side, view, new(0, 0, 1, 1));

    /// <summary>Makes a face whose corners also name the first diffuse colour.</summary>
    /// <param name="a">The first position.</param>
    /// <param name="b">The second position.</param>
    /// <param name="c">The third position.</param>
    /// <returns>The face.</returns>
    private static U3dFaceSpec Coloured(uint a, uint b, uint c) => new(0, [a, b, c], [0, 0, 0], [0, 0, 0], []);

    /// <summary>Makes a face whose corners also name the texture coordinate of the same index.</summary>
    /// <param name="a">The first position.</param>
    /// <param name="b">The second position.</param>
    /// <param name="c">The third position.</param>
    /// <returns>The face.</returns>
    private static U3dFaceSpec Textured(uint a, uint b, uint c) => new(0, [a, b, c], [0, 0, 0], [], [a, b, c]);

    /// <summary>Makes a face with no normals.</summary>
    /// <param name="a">The first position.</param>
    /// <param name="b">The second position.</param>
    /// <param name="c">The third position.</param>
    /// <returns>The face.</returns>
    private static U3dFaceSpec Plain(uint a, uint b, uint c) => new(0, [a, b, c], [], [], []);
}
