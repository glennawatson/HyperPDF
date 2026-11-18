// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Cpu;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// Checks that a small picture the CPU backend keeps at the size it is drawn at looks exactly as it does when scaled afresh, and
/// that a kept picture is never shown in place of new pixels.
/// </summary>
public sealed class ScaledCopyCacheTests
{
    /// <summary>The frame's width and height in pixels.</summary>
    private const int FrameSize = 16;

    /// <summary>The picture's width and height in pixels.</summary>
    private const int PictureSize = 8;

    /// <summary>The width the picture is drawn at.</summary>
    private const int DrawnWidth = 5;

    /// <summary>The height the picture is drawn at.</summary>
    private const int DrawnHeight = 6;

    /// <summary>Where the picture is drawn, across and down.</summary>
    private const int DrawnAt = 3;

    /// <summary>The first column and row of the clip, which cuts through the picture.</summary>
    private const int ClipStart = 5;

    /// <summary>The clip's width and height.</summary>
    private const int ClipSize = 8;

    /// <summary>The frames drawn, enough for the scaling to be seen, kept and then copied.</summary>
    private const int Frames = 4;

    /// <summary>The bytes of one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the alpha byte in BGRA.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The step between the values of neighbouring pixels, so every scaled pixel is a mix.</summary>
    private const int Shade = 29;

    /// <summary>The byte value of a full channel.</summary>
    private const byte Full = 255;

    /// <summary>Opaque white.</summary>
    private const uint White = 0xFFFFFFFF;

    /// <summary>Every frame of the same scaled picture is the same, whether it was scaled for the frame or copied from the kept scaling.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeptScalingLooksLikeAFreshOne()
    {
        using var backend = NewBackend();
        using var picture = backend.Surfaces.Create(PictureSize, PictureSize);
        Paint(picture, 0);

        var first = DrawFrame(backend, picture, false);
        var same = true;
        for (var frame = 1; frame < Frames; frame++)
        {
            same &= first.AsSpan().SequenceEqual(DrawFrame(backend, picture, false));
        }

        await Assert.That(same).IsTrue();
    }

    /// <summary>A kept scaling drawn under a clip that cuts through it fills the same pixels as a fresh one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeptScalingIsClippedLikeAFreshOne()
    {
        using var backend = NewBackend();
        using var picture = backend.Surfaces.Create(PictureSize, PictureSize);
        Paint(picture, 0);

        var first = DrawFrame(backend, picture, true);
        var same = true;
        for (var frame = 1; frame < Frames; frame++)
        {
            same &= first.AsSpan().SequenceEqual(DrawFrame(backend, picture, true));
        }

        await Assert.That(same).IsTrue();
    }

    /// <summary>A picture written again after its scaling was kept is shown with its new pixels at once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PictureWrittenAgainIsScaledAgain()
    {
        using var backend = NewBackend();
        using var picture = backend.Surfaces.Create(PictureSize, PictureSize);
        Paint(picture, 0);
        for (var frame = 0; frame < Frames; frame++)
        {
            _ = DrawFrame(backend, picture, false);
        }

        Paint(picture, 1);
        var after = DrawFrame(backend, picture, false);

        using var fresh = NewBackend();
        using var repainted = fresh.Surfaces.Create(PictureSize, PictureSize);
        Paint(repainted, 1);
        var expected = DrawFrame(fresh, repainted, false);

        await Assert.That(after.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>
    /// A picture left out of some frames, as the pictures of a tab the reader moved away from are, looks the same when it is drawn
    /// again from the scaling that was kept meanwhile.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PictureLeftOutOfFramesLooksTheSameAfter()
    {
        using var backend = NewBackend();
        using var picture = backend.Surfaces.Create(PictureSize, PictureSize);
        Paint(picture, 0);
        var first = DrawFrame(backend, picture, false);
        for (var frame = 1; frame < Frames; frame++)
        {
            _ = DrawFrame(backend, picture, false);
        }

        for (var frame = 0; frame < Frames; frame++)
        {
            DrawEmptyFrame(backend);
        }

        await Assert.That(first.AsSpan().SequenceEqual(DrawFrame(backend, picture, false))).IsTrue();
    }

    /// <summary>A picture written again while no frame drew it is shown with its new pixels, not with the scaling kept from before.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PictureWrittenWhileLeftOutIsScaledAgain()
    {
        using var backend = NewBackend();
        using var picture = backend.Surfaces.Create(PictureSize, PictureSize);
        Paint(picture, 0);
        for (var frame = 0; frame < Frames; frame++)
        {
            _ = DrawFrame(backend, picture, false);
        }

        DrawEmptyFrame(backend);
        Paint(picture, 1);
        DrawEmptyFrame(backend);
        var after = DrawFrame(backend, picture, false);

        using var fresh = NewBackend();
        using var repainted = fresh.Surfaces.Create(PictureSize, PictureSize);
        Paint(repainted, 1);
        var expected = DrawFrame(fresh, repainted, false);

        await Assert.That(after.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>Draws one frame with nothing but its background.</summary>
    /// <param name="backend">The backend.</param>
    private static void DrawEmptyFrame(CpuRenderBackend backend)
    {
        using var pass = backend.BeginFrame();
        pass.Clear(White);
    }

    /// <summary>Makes a backend with a small frame.</summary>
    /// <returns>The backend.</returns>
    private static CpuRenderBackend NewBackend()
    {
        var backend = new CpuRenderBackend(static (_, _, _) => { });
        backend.Resize(new(FrameSize, FrameSize));
        return backend;
    }

    /// <summary>Fills a picture with opaque pixels that all differ from their neighbours.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="shift">A number that moves every value, so two paintings differ.</param>
    private static void Paint(IRenderSurface picture, int shift) =>
        _ = picture.Write(shift, static (target, in moved) =>
        {
            for (var pixel = 0; pixel < target.Pixels.Length; pixel += BytesPerPixel)
            {
                for (var channel = 0; channel < AlphaOffset; channel++)
                {
                    target.Pixels[pixel + channel] = (byte)((pixel + channel + moved) * Shade);
                }

                target.Pixels[pixel + AlphaOffset] = Full;
            }

            return true;
        });

    /// <summary>Draws one frame with the picture smoothly scaled down and returns the frame's pixels.</summary>
    /// <param name="backend">The backend.</param>
    /// <param name="picture">The picture.</param>
    /// <param name="clipped">Whether a clip cuts through the picture.</param>
    /// <returns>The frame's pixels.</returns>
    private static byte[] DrawFrame(CpuRenderBackend backend, IRenderSurface picture, bool clipped)
    {
        using (var pass = backend.BeginFrame())
        {
            pass.Clear(White);
            if (clipped)
            {
                pass.SetClip(new(ClipStart, ClipStart, ClipSize, ClipSize));
            }

            pass.DrawSurface(picture, new(DrawnAt, DrawnAt, DrawnWidth, DrawnHeight), true);
        }

        return backend.FramePixels.ToArray();
    }
}
