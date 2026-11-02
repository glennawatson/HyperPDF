// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Media.U3d;
using HyperPdfLibrary.Rendering;
using SkiaSharp;
using static HyperPdfLibrary.Tests.Media.U3dTestScenes;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Checks that the triangles of a U3D frame are painted onto a Skia canvas, which a GPU-backed canvas draws on the graphics card.</summary>
public sealed class SkiaU3dPainterTests
{
    /// <summary>The side of the test frame.</summary>
    private const int Side = 64;

    /// <summary>The centre pixel.</summary>
    private const int Centre = 32;

    /// <summary>The pixel offset inside one quarter of the quad.</summary>
    private const int InsideQuad = 8;

    /// <summary>The side of the texture.</summary>
    private const uint TextureSide = 2;

    /// <summary>The name of the texture.</summary>
    private const string Checker = "Checker";

    /// <summary>The PNG compression type.</summary>
    private const byte Png = 2;

    /// <summary>Every channel a continuation image supplies.</summary>
    private const byte AllChannels = 0x1F;

    /// <summary>A strong channel.</summary>
    private const byte Strong = 200;

    /// <summary>A weak channel.</summary>
    private const byte Weak = 60;

    /// <summary>The half opacity of the transparent test.</summary>
    private const float HalfOpacity = 0.5F;

    /// <summary>A red quad is painted red on the background.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PaintsTheQuad()
    {
        using var bitmap = Paint(U3dModelReader.Read(RedQuad(false)));

        var centre = bitmap.GetPixel(Centre, Centre);
        var corner = bitmap.GetPixel(1, 1);

        await Assert.That(centre.Red).IsGreaterThan(Strong);
        await Assert.That(centre.Blue).IsLessThan(Weak);
        await Assert.That(corner.Blue).IsGreaterThan(Strong);
        await Assert.That(corner.Red).IsLessThan(Weak);
    }

    /// <summary>A textured quad shows each quarter of its texture.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PaintsATexture()
    {
        var quad = Quad(QuadResource, QuadCorners) with
        {
            TextureCoordinates = [new(0, 1, 0, 0), new(1, 1, 0, 0), new(1, 0, 0, 0), new(0, 0, 0, 0)],
            Faces = [Textured(CornerA, CornerB, CornerC), Textured(CornerA, CornerC, CornerD)],
            Shadings = [new(false, [TextureSide])],
        };
        byte[] pixels = [255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255];
        var writer = new U3dFileWriter(false);
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

        using var bitmap = Paint(U3dModelReader.Read(file));

        var topLeft = bitmap.GetPixel(Centre - InsideQuad, Centre - InsideQuad);
        var topRight = bitmap.GetPixel(Centre + InsideQuad, Centre - InsideQuad);
        var bottomLeft = bitmap.GetPixel(Centre - InsideQuad, Centre + InsideQuad);
        await Assert.That(topLeft.Red).IsGreaterThan(Strong);
        await Assert.That(topLeft.Green).IsLessThan(Weak);
        await Assert.That(topRight.Green).IsGreaterThan(Strong);
        await Assert.That(topRight.Red).IsLessThan(Weak);
        await Assert.That(bottomLeft.Blue).IsGreaterThan(Strong);
        await Assert.That(bottomLeft.Red).IsLessThan(Weak);
    }

    /// <summary>A transparent material blends with the background.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PaintsTransparency()
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

        using var bitmap = Paint(U3dModelReader.Read(file));

        var centre = bitmap.GetPixel(Centre, Centre);
        await Assert.That(centre.Red).IsBetween(Weak, Strong);
        await Assert.That(centre.Blue).IsBetween(Weak, Strong);
    }

    /// <summary>Missing arguments are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksItsArguments()
    {
        using var bitmap = new SKBitmap(Side, Side);
        using var canvas = new SKCanvas(bitmap);
        var list = U3dRenderer.Collect(U3dModelReader.Read(RedQuad(false)), Options(), CancellationToken.None);

        await Assert.That(() => SkiaU3dPainter.Paint(null!, list)).Throws<ArgumentNullException>();
        await Assert.That(() => SkiaU3dPainter.Paint(canvas, null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Makes the standard options: a blue background.</summary>
    /// <returns>The options.</returns>
    private static U3dRenderOptions Options() => new(Side, Side, null, new(0, 0, 1, 1));

    /// <summary>Paints a model onto a raster bitmap.</summary>
    /// <param name="model">The model.</param>
    /// <returns>The bitmap.</returns>
    private static SKBitmap Paint(U3dModel model)
    {
        var bitmap = new SKBitmap(Side, Side);
        using var canvas = new SKCanvas(bitmap);
        SkiaU3dPainter.Paint(canvas, U3dRenderer.Collect(model, Options(), CancellationToken.None));
        return bitmap;
    }

    /// <summary>Makes a face whose corners also name the texture coordinate of the same index.</summary>
    /// <param name="a">The first position.</param>
    /// <param name="b">The second position.</param>
    /// <param name="c">The third position.</param>
    /// <returns>The face.</returns>
    private static U3dFaceSpec Textured(uint a, uint b, uint c) => new(0, [a, b, c], [0, 0, 0], [], [a, b, c]);
}
