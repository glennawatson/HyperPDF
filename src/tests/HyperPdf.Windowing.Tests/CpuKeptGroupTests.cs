// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// Checks that a group of draws whose pixels the CPU backend keeps between frames always shows what drawing it afresh shows:
/// while it stays the same, after a picture in it is written again, after its draws change, and when a draw in it lets the frame
/// under it show.
/// </summary>
public sealed class CpuKeptGroupTests
{
    /// <summary>The frame's width in pixels.</summary>
    private const int Width = 64;

    /// <summary>The frame's height in pixels.</summary>
    private const int Height = 48;

    /// <summary>The height of the bar the group draws.</summary>
    private const int BarHeight = 16;

    /// <summary>The width and height of the picture drawn in the bar.</summary>
    private const int PictureSize = 8;

    /// <summary>Where the picture is drawn in the bar, across and down.</summary>
    private const int PictureAt = 4;

    /// <summary>The frames drawn, enough for the group to settle, be kept and be put back several times.</summary>
    private const int Frames = 8;

    /// <summary>The frame at which the picture or the draws change.</summary>
    private const int ChangeAt = 5;

    /// <summary>The step between the colours behind the group from one frame to the next.</summary>
    private const uint BehindStep = 0x00050709;

    /// <summary>The first colour behind the group.</summary>
    private const uint Behind = 0xFF203040;

    /// <summary>The opaque colour of the bar.</summary>
    private const uint Bar = 0xFFE0E0E0;

    /// <summary>The see-through colour drawn over part of the bar.</summary>
    private const uint Highlight = 0x403060C0;

    /// <summary>The see-through colour drawn below the bar, where the frame behind shows through it.</summary>
    private const uint Shade = 0x60101010;

    /// <summary>The byte the picture's channels hold first.</summary>
    private const byte FirstInk = 0x30;

    /// <summary>The byte the picture's channels hold after it is written again.</summary>
    private const byte SecondInk = 0x70;

    /// <summary>The alpha of the picture, which lets the bar show through it.</summary>
    private const byte PictureAlpha = 0x80;

    /// <summary>The bytes of one pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the alpha byte in a pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>What changes part way through the frames.</summary>
    public enum Change
    {
        /// <summary>Nothing changes.</summary>
        None = 0,

        /// <summary>The picture is written again with other pixels.</summary>
        PictureWritten = 1,

        /// <summary>The highlight moves.</summary>
        DrawsMoved = 2,
    }

    /// <summary>Every frame of a kept group is the frame drawing it afresh gives, whatever is behind it and whatever changes in it.</summary>
    /// <param name="change">What changes part way through.</param>
    /// <param name="showsBehind">Whether the group also draws a see-through fill over the frame behind it.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Change.None, false)]
    [Arguments(Change.PictureWritten, false)]
    [Arguments(Change.DrawsMoved, false)]
    [Arguments(Change.None, true)]
    [Arguments(Change.PictureWritten, true)]
    public async Task KeptGroupLooksLikeAFreshOne(Change change, bool showsBehind)
    {
        using var keeping = NewBackend();
        using var fresh = NewBackend();
        using var keptPicture = NewPicture(keeping);
        using var freshPicture = NewPicture(fresh);
        var differences = 0;
        for (var frame = 0; frame < Frames; frame++)
        {
            if (frame == ChangeAt && change == Change.PictureWritten)
            {
                Fill(keptPicture, SecondInk);
                Fill(freshPicture, SecondInk);
            }

            var moved = frame >= ChangeAt && change == Change.DrawsMoved;
            var behind = Behind + ((uint)frame * BehindStep);
            using (var pass = keeping.BeginFrame())
            {
                pass.Clear(behind);
                _ = DrawKeptGroup(pass, keptPicture, moved, showsBehind);
            }

            using (var pass = fresh.BeginFrame())
            {
                pass.Clear(behind);
                DrawGroup(pass, freshPicture, moved, showsBehind);
            }

            differences += CountDifferences(keeping.FramePixels, fresh.FramePixels);
        }

        await Assert.That(differences).IsEqualTo(0);
    }

    /// <summary>A group that stays the same is put back from the kept pixels, and one that shows the frame behind it never is.</summary>
    /// <param name="showsBehind">Whether the group draws a see-through fill over the frame behind it.</param>
    /// <param name="expected">Whether the kept pixels are put back.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false, true)]
    [Arguments(true, false)]
    public async Task OnlyAGroupThatDecidesItsOwnPixelsIsPutBack(bool showsBehind, bool expected)
    {
        using var backend = NewBackend();
        using var picture = NewPicture(backend);
        for (var frame = 0; frame < Frames; frame++)
        {
            // What is behind the group changes each frame, so the frame is drawn and only the group can be put back.
            using var pass = backend.BeginFrame();
            pass.Clear(Behind + ((uint)frame * BehindStep));
            _ = DrawKeptGroup(pass, picture, false, showsBehind);
        }

        await Assert.That(backend.KeptRestores > 0).IsEqualTo(expected);
    }

    /// <summary>A frame whose draws are those of the frame before is not drawn again, and a frame with any other draw is.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyAnUnchangedFrameIsLeftUndrawn()
    {
        using var backend = NewBackend();
        using var fresh = NewBackend();
        using var picture = NewPicture(backend);
        using var freshPicture = NewPicture(fresh);
        var differences = 0;
        var skippedWhileSame = 0;
        var skippedAfterWrite = 0;
        for (var frame = 0; frame < Frames; frame++)
        {
            if (frame == ChangeAt)
            {
                skippedWhileSame = backend.UnchangedFrames;
                Fill(picture, SecondInk);
                Fill(freshPicture, SecondInk);
            }

            using (var pass = backend.BeginFrame())
            {
                pass.Clear(Behind);
                DrawGroup(pass, picture, false, true);
            }

            if (frame == ChangeAt)
            {
                skippedAfterWrite = backend.UnchangedFrames - skippedWhileSame;
            }

            // The other backend is given a different frame in between, so it draws every frame afresh.
            using (var pass = fresh.BeginFrame())
            {
                pass.Clear(Bar);
            }

            using (var pass = fresh.BeginFrame())
            {
                pass.Clear(Behind);
                DrawGroup(pass, freshPicture, false, true);
            }

            differences += CountDifferences(backend.FramePixels, fresh.FramePixels);
        }

        await Assert.That(differences).IsEqualTo(0);
        await Assert.That(skippedWhileSame).IsEqualTo(ChangeAt - 1);
        await Assert.That(skippedAfterWrite).IsEqualTo(0);
        await Assert.That(fresh.UnchangedFrames).IsEqualTo(0);
    }

    /// <summary>A frame that does not start by clearing is drawn every time, because its pixels also depend on what was there.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FrameThatDoesNotClearIsAlwaysDrawn()
    {
        using var backend = NewBackend();
        for (var frame = 0; frame < Frames; frame++)
        {
            using var pass = backend.BeginFrame();
            pass.FillRect(new(0, 0, Width, Height), Shade);
        }

        await Assert.That(backend.UnchangedFrames).IsEqualTo(0);
    }

    /// <summary>Draws the group the way the window draws its controls: a trial first, and for real when the kept pixels do not match.</summary>
    /// <param name="pass">The frame pass.</param>
    /// <param name="picture">The picture.</param>
    /// <param name="moved">Whether the highlight is in its second place.</param>
    /// <param name="showsBehind">Whether a see-through fill is drawn over the frame behind the group.</param>
    /// <returns><see langword="true"/> when the kept pixels were put back.</returns>
    private static bool DrawKeptGroup(IFramePass pass, IRenderSurface picture, bool moved, bool showsBehind)
    {
        if (pass.BeginKeptTrial())
        {
            DrawGroup(pass, picture, moved, showsBehind);
            if (pass.EndKeptTrial())
            {
                return true;
            }
        }

        pass.BeginKeptGroup();
        DrawGroup(pass, picture, moved, showsBehind);
        pass.EndKeptGroup();
        return false;
    }

    /// <summary>Draws the group: an opaque bar, a see-through highlight and a see-through picture on it, and perhaps a shade below it.</summary>
    /// <param name="pass">The frame pass.</param>
    /// <param name="picture">The picture.</param>
    /// <param name="moved">Whether the highlight is in its second place.</param>
    /// <param name="showsBehind">Whether a see-through fill is drawn over the frame behind the group.</param>
    private static void DrawGroup(IFramePass pass, IRenderSurface picture, bool moved, bool showsBehind)
    {
        pass.FillRect(new(0, 0, Width, BarHeight), Bar);
        pass.FillRect(new(moved ? PictureSize + PictureSize : PictureSize, 0, PictureSize, BarHeight), Highlight);
        pass.DrawSurface(picture, new(PictureAt, PictureAt, PictureSize, PictureSize), false);
        if (showsBehind)
        {
            pass.FillRect(new(0, BarHeight, Width, BarHeight), Shade);
        }
    }

    /// <summary>Makes a backend with a frame.</summary>
    /// <returns>The backend.</returns>
    private static CpuRenderBackend NewBackend()
    {
        var backend = new CpuRenderBackend(static (_, _, _) => { });
        backend.Resize(new(Width, Height));
        return backend;
    }

    /// <summary>Makes a see-through picture.</summary>
    /// <param name="backend">The backend that draws it.</param>
    /// <returns>The picture.</returns>
    private static IRenderSurface NewPicture(CpuRenderBackend backend)
    {
        var picture = backend.Surfaces.Create(PictureSize, PictureSize);
        Fill(picture, FirstInk);
        return picture;
    }

    /// <summary>Writes a picture's pixels.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="ink">The byte its colour channels hold.</param>
    private static void Fill(IRenderSurface picture, byte ink) =>
        _ = picture.Write(ink, static (target, in value) =>
        {
            target.Pixels.Fill(value);
            for (var i = AlphaOffset; i < target.Pixels.Length; i += BytesPerPixel)
            {
                target.Pixels[i] = PictureAlpha;
            }

            return true;
        });

    /// <summary>Counts the bytes that differ between two frames.</summary>
    /// <param name="first">One frame.</param>
    /// <param name="second">The other.</param>
    /// <returns>The number of bytes that differ.</returns>
    private static int CountDifferences(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var differences = 0;
        for (var i = 0; i < first.Length; i++)
        {
            if (first[i] != second[i])
            {
                differences++;
            }
        }

        return differences;
    }
}
