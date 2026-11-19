// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Cpu;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the first frames that show new small pictures smoothly scaled down, as the side panel does each time a document
/// opens or a tab is shown for the first time. The pictures are written again before every run, so none of their scalings is
/// kept from the run before.
/// </summary>
public class SidePanelPicturesFirstShownBenchmarks
{
    /// <summary>The frame's width in pixels.</summary>
    private const int FrameWidth = 1920;

    /// <summary>The frame's height in pixels.</summary>
    private const int FrameHeight = 1080;

    /// <summary>The width of a page picture as it is rendered.</summary>
    private const int PictureWidth = 306;

    /// <summary>The height of a page picture as it is rendered.</summary>
    private const int PictureHeight = 396;

    /// <summary>The width a page picture is drawn at in the panel.</summary>
    private const int DrawnWidth = 150;

    /// <summary>The height a page picture is drawn at in the panel.</summary>
    private const int DrawnHeight = 194;

    /// <summary>The pictures the panel shows at once.</summary>
    private const int Pictures = 5;

    /// <summary>The space from the frame's edge and between pictures.</summary>
    private const int Gap = 12;

    /// <summary>The frames drawn: the one that first shows the pictures and the two after it.</summary>
    private const int Frames = 3;

    /// <summary>The bytes of one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The step between the values of neighbouring pixels, so the pictures are not flat.</summary>
    private const int Shade = 29;

    /// <summary>The frame's background.</summary>
    private const uint Canvas = 0xFF303030;

    /// <summary>The backend.</summary>
    private CpuRenderBackend _backend = null!;

    /// <summary>The page pictures.</summary>
    private IRenderSurface[] _pictures = [];

    /// <summary>Makes the backend and the pictures.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _backend = new(static (_, _, _) => { });
        _backend.Resize(new(FrameWidth, FrameHeight));
        _pictures = new IRenderSurface[Pictures];
        for (var i = 0; i < _pictures.Length; i++)
        {
            _pictures[i] = _backend.Surfaces.Create(PictureWidth, PictureHeight);
            _ = _pictures[i].Write(i, static (target, in shift) =>
            {
                for (var pixel = 0; pixel < target.Pixels.Length; pixel += BytesPerPixel)
                {
                    for (var channel = 0; channel < BytesPerPixel - 1; channel++)
                    {
                        target.Pixels[pixel + channel] = (byte)((pixel + channel + shift) * Shade);
                    }

                    target.Pixels[pixel + BytesPerPixel - 1] = byte.MaxValue;
                }

                return true;
            });
        }
    }

    /// <summary>Releases the pictures and the backend.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var picture in _pictures)
        {
            picture.Dispose();
        }

        _backend.Dispose();
    }

    /// <summary>Shows pictures that were just written for three frames: the frame that first scales them and two that follow.</summary>
    [Benchmark]
    public void ShowNewPicturesForThreeFrames()
    {
        // Writing a picture, even with nothing changed, makes it new to the backend, as a picture just rendered is.
        foreach (var picture in _pictures)
        {
            _ = picture.Write(0, static (_, in _) => true);
        }

        for (var frame = 0; frame < Frames; frame++)
        {
            using var pass = _backend.BeginFrame();
            pass.Clear(Canvas);
            for (var i = 0; i < _pictures.Length; i++)
            {
                pass.DrawSurface(_pictures[i], new(Gap, Gap + (i * (DrawnHeight + Gap)), DrawnWidth, DrawnHeight), true);
            }
        }
    }
}
