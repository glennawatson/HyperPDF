// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Rendering.Direct3D;
using HyperPdf.Rendering.Direct3D12;
using HyperPdf.Rendering.Vulkan;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures one whole frame of the page canvas on the CPU and Vulkan render backends: a clear, a grid of cached tiles and a translucent selection fill.</summary>
public class RenderBackendFrameBenchmarks
{
    /// <summary>The frame width in pixels.</summary>
    private const int FrameWidth = 1920;

    /// <summary>The frame height in pixels.</summary>
    private const int FrameHeight = 1080;

    /// <summary>The tile edge in pixels, matching the canvas tile grid.</summary>
    private const int TileEdge = 512;

    /// <summary>The tile columns that cover the frame.</summary>
    private const int Columns = 4;

    /// <summary>The tile rows that cover the frame.</summary>
    private const int Rows = 3;

    /// <summary>The zoom of one wheel notch, for stretched tiles.</summary>
    private const double WheelStep = 1.1;

    /// <summary>The opaque canvas colour behind the pages.</summary>
    private const uint Canvas = 0xFF303030;

    /// <summary>The translucent selection colour.</summary>
    private const uint Selection = 0x663388FF;

    /// <summary>The byte written into every channel of the tiles: opaque white paper, the common page tile.</summary>
    private const byte Paper = 0xFF;

    /// <summary>How much the faded layer counts.</summary>
    private const double HalfOpacity = 0.5;

    /// <summary>The left edge of the selection.</summary>
    private const double SelectionLeft = 200;

    /// <summary>The top edge of the selection.</summary>
    private const double SelectionTop = 300;

    /// <summary>The width of the selection.</summary>
    private const double SelectionWidth = 900;

    /// <summary>The height of the selection.</summary>
    private const double SelectionHeight = 24;

    /// <summary>The width of the side panel the pages fade beside, in pixels.</summary>
    private const int PanelWidth = 280;

    /// <summary>The height of the bars the pages fade under, in pixels.</summary>
    private const int BarHeight = 96;

    /// <summary>The rows from the top of one line of text to the top of the next.</summary>
    private const int LinePitch = 20;

    /// <summary>The rows of a line of text that hold ink.</summary>
    private const int LineHeight = 8;

    /// <summary>The paper at each side of the lines, in pixels.</summary>
    private const int TextMargin = 48;

    /// <summary>The width of one run of ink, and of the paper after it, in pixels.</summary>
    private const int InkRun = 3;

    /// <summary>One run in this many is ink.</summary>
    private const int InkEvery = 2;

    /// <summary>The bytes of one pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The colour bytes of one pixel, before its alpha.</summary>
    private const int ColourBytes = 3;

    /// <summary>The byte written into the colour channels of ink.</summary>
    private const byte Ink = 0x20;

    /// <summary>The frames drawn so far, which picks the canvas colour of the next one.</summary>
    private uint _frames;

    /// <summary>The CPU backend.</summary>
    private CpuRenderBackend _cpu = null!;

    /// <summary>The CPU tiles.</summary>
    private IRenderSurface[] _cpuTiles = [];

    /// <summary>The CPU tiles that hold lines of text on paper, as a page does.</summary>
    private IRenderSurface[] _cpuTextTiles = [];

    /// <summary>The Vulkan backend, or <see langword="null"/> on a machine with no Vulkan driver.</summary>
    private VulkanRenderBackend? _gpu;

    /// <summary>The Vulkan tiles.</summary>
    private IRenderSurface[] _gpuTiles = [];

    /// <summary>The Direct3D backend, or <see langword="null"/> off Windows.</summary>
    private IRenderBackend? _direct3D;

    /// <summary>The Direct3D tiles.</summary>
    private IRenderSurface[] _direct3DTiles = [];

    /// <summary>The Direct3D 12 backend, or <see langword="null"/> off Windows.</summary>
    private IRenderBackend? _direct3D12;

    /// <summary>The Direct3D 12 tiles.</summary>
    private IRenderSurface[] _direct3D12Tiles = [];

    /// <summary>Creates both backends and their filled tiles, and draws one frame so tiles are uploaded.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _cpu = new(static (_, _, _) => { });
        _cpu.Resize(new(FrameWidth, FrameHeight));
        _cpuTiles = CreateTiles(_cpu);
        _cpuTextTiles = CreateTextTiles(_cpu);
        if (VulkanContext.TryCreate() is { } context)
        {
            _gpu = new(context);
            _gpu.Resize(new(FrameWidth, FrameHeight));
            _gpuTiles = CreateTiles(_gpu);
            DrawFrame(_gpu, _gpuTiles, 1, smooth: false);
        }

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _direct3D = new Direct3DRenderBackend();
        _direct3D.Resize(new(FrameWidth, FrameHeight));
        _direct3DTiles = CreateTiles(_direct3D);
        DrawFrame(_direct3D, _direct3DTiles, 1, smooth: false);

        _direct3D12 = new Direct3D12RenderBackend();
        _direct3D12.Resize(new(FrameWidth, FrameHeight));
        _direct3D12Tiles = CreateTiles(_direct3D12);
        DrawFrame(_direct3D12, _direct3D12Tiles, 1, smooth: false);
    }

    /// <summary>Releases the tiles and the backends.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        DisposeTiles(_cpuTiles);
        DisposeTiles(_cpuTextTiles);
        DisposeTiles(_gpuTiles);
        DisposeTiles(_direct3DTiles);
        DisposeTiles(_direct3D12Tiles);
        _cpu.Dispose();
        _gpu?.Dispose();
        _direct3D?.Dispose();
        _direct3D12?.Dispose();
    }

    /// <summary>Draws a frame of unscaled tiles on the CPU, as on every scroll step.</summary>
    [Benchmark(Baseline = true)]
    public void CpuFrame() => DrawFrame(_cpu, _cpuTiles, 1, smooth: false);

    /// <summary>Draws a frame of tiles stretched by one wheel notch on the CPU with nearest sampling.</summary>
    [Benchmark]
    public void CpuFrameStretchedNearest() => DrawFrame(_cpu, _cpuTiles, WheelStep, smooth: false);

    /// <summary>Draws a frame of tiles stretched by one wheel notch on the CPU with smooth sampling, as during a zoom.</summary>
    [Benchmark]
    public void CpuFrameStretchedSmooth() => DrawFrame(_cpu, _cpuTiles, WheelStep, smooth: true);

    /// <summary>Draws a frame of tiles that hold lines of text, stretched by one wheel notch on the CPU by picking the nearest pixel.</summary>
    [Benchmark]
    public void CpuFrameStretchedNearestText() => DrawFrame(_cpu, _cpuTextTiles, WheelStep, smooth: false);

    /// <summary>Draws a frame of tiles that hold lines of text, stretched by one wheel notch on the CPU with smooth sampling.</summary>
    [Benchmark]
    public void CpuFrameStretchedSmoothText() => DrawFrame(_cpu, _cpuTextTiles, WheelStep, smooth: true);

    /// <summary>Draws a frame whose tiles are a half opaque layer on the CPU, as a page fade does: the frame is copied before the layer and mixed with it after.</summary>
    [Benchmark]
    public void CpuFrameFaded() => DrawFadedFrame(_cpu, _cpuTiles);

    /// <summary>Draws a frame on the CPU where only the pages fade, beside a side panel and under a tool bar that do not.</summary>
    [Benchmark]
    public void CpuFramePagesFaded()
    {
        using var pass = _cpu.BeginFrame();
        pass.Clear(NextCanvas());
        pass.SetOpacity(HalfOpacity);
        for (var i = 0; i < _cpuTiles.Length; i++)
        {
            var row = Math.DivRem(i, Columns, out var column);
            pass.DrawSurface(_cpuTiles[i], new(PanelWidth + (column * TileEdge), BarHeight + (row * TileEdge), TileEdge, TileEdge), smooth: false);
        }

        pass.SetOpacity(1);
    }

    /// <summary>Draws a frame whose tiles are a half opaque layer on the GPU, which scales the colour of each tile and costs nothing more per frame.</summary>
    [Benchmark]
    public void VulkanFrameFaded()
    {
        if (_gpu is { } gpu)
        {
            DrawFadedFrame(gpu, _gpuTiles);
        }
    }

    /// <summary>Draws the same frame of unscaled cached tiles on the GPU as the frame before it, which the backend shows again without drawing it.</summary>
    [Benchmark]
    public void VulkanFrameUnchanged()
    {
        if (_gpu is { } gpu)
        {
            DrawFrame(gpu, _gpuTiles, 1, smooth: false, Canvas);
        }
    }

    /// <summary>Draws a frame of unscaled cached tiles on the GPU and waits for it.</summary>
    [Benchmark]
    public void VulkanFrame()
    {
        if (_gpu is { } gpu)
        {
            DrawFrame(gpu, _gpuTiles, 1, smooth: false);
        }
    }

    /// <summary>Draws a frame of smoothly stretched cached tiles on the GPU and waits for it.</summary>
    [Benchmark]
    public void VulkanFrameStretchedSmooth()
    {
        if (_gpu is { } gpu)
        {
            DrawFrame(gpu, _gpuTiles, WheelStep, smooth: true);
        }
    }

    /// <summary>Records a frame of unscaled cached tiles and hands it to Direct3D. The GPU draws it after the call returns, so this is the cost to the window thread.</summary>
    [Benchmark]
    public void Direct3DFrame()
    {
        if (_direct3D is { } gpu)
        {
            DrawFrame(gpu, _direct3DTiles, 1, smooth: false);
        }
    }

    /// <summary>Records a frame of smoothly stretched cached tiles and hands it to Direct3D.</summary>
    [Benchmark]
    public void Direct3DFrameStretchedSmooth()
    {
        if (_direct3D is { } gpu)
        {
            DrawFrame(gpu, _direct3DTiles, WheelStep, smooth: true);
        }
    }

    /// <summary>Draws a frame of unscaled cached tiles with Direct3D 12 and waits for the GPU, which Direct3D 12 frames do at their end.</summary>
    [Benchmark]
    public void Direct3D12Frame()
    {
        if (_direct3D12 is { } gpu)
        {
            DrawFrame(gpu, _direct3D12Tiles, 1, smooth: false);
        }
    }

    /// <summary>Draws a frame of smoothly stretched cached tiles with Direct3D 12 and waits for the GPU.</summary>
    [Benchmark]
    public void Direct3D12FrameStretchedSmooth()
    {
        if (_direct3D12 is { } gpu)
        {
            DrawFrame(gpu, _direct3D12Tiles, WheelStep, smooth: true);
        }
    }

    /// <summary>Creates the tile grid of a backend and fills every tile with opaque white.</summary>
    /// <param name="backend">The backend.</param>
    /// <returns>The tiles, row by row.</returns>
    private static IRenderSurface[] CreateTiles(IRenderBackend backend)
    {
        var tiles = new IRenderSurface[Columns * Rows];
        for (var i = 0; i < tiles.Length; i++)
        {
            tiles[i] = backend.Surfaces.Create(TileEdge, TileEdge);
            _ = tiles[i].Write(0, static (target, in _) =>
            {
                target.Pixels.Fill(Paper);
                return true;
            });
        }

        return tiles;
    }

    /// <summary>Creates the tile grid of a backend and fills every tile with lines of text on paper.</summary>
    /// <param name="backend">The backend.</param>
    /// <returns>The tiles, row by row.</returns>
    private static IRenderSurface[] CreateTextTiles(CpuRenderBackend backend)
    {
        var tiles = new IRenderSurface[Columns * Rows];
        for (var i = 0; i < tiles.Length; i++)
        {
            tiles[i] = backend.Surfaces.Create(TileEdge, TileEdge);
            _ = tiles[i].Write(0, static (target, in _) =>
            {
                target.Pixels.Fill(Paper);
                DrawLines(target.Pixels);
                return true;
            });
        }

        return tiles;
    }

    /// <summary>Draws lines of short ink runs with paper between the lines and at the sides, as text lies on a page.</summary>
    /// <param name="pixels">The pixels of one tile.</param>
    private static void DrawLines(Span<byte> pixels)
    {
        for (var y = 0; y < TileEdge; y++)
        {
            if (y % LinePitch >= LineHeight)
            {
                continue;
            }

            for (var x = TextMargin; x < TileEdge - TextMargin; x++)
            {
                if ((x / InkRun) % InkEvery == 0)
                {
                    pixels.Slice(((y * TileEdge) + x) * BytesPerPixel, ColourBytes).Fill(Ink);
                }
            }
        }
    }

    /// <summary>Disposes every tile.</summary>
    /// <param name="tiles">The tiles.</param>
    private static void DisposeTiles(IRenderSurface[] tiles)
    {
        foreach (var tile in tiles)
        {
            tile.Dispose();
        }
    }

    /// <summary>Draws one frame: a canvas colour, the tile grid and a selection.</summary>
    /// <param name="backend">The backend.</param>
    /// <param name="tiles">The tiles, row by row.</param>
    /// <param name="scale">How much the tiles are stretched.</param>
    /// <param name="smooth">Whether stretched tiles are sampled smoothly.</param>
    /// <param name="canvas">The colour the frame is cleared to.</param>
    private static void DrawFrame(IRenderBackend backend, IRenderSurface[] tiles, double scale, bool smooth, uint canvas)
    {
        using var pass = backend.BeginFrame();
        pass.Clear(canvas);
        var edge = TileEdge * scale;
        for (var i = 0; i < tiles.Length; i++)
        {
            var row = Math.DivRem(i, Columns, out var column);
            pass.DrawSurface(tiles[i], new(column * edge, row * edge, edge, edge), smooth);
        }

        pass.FillRect(new(SelectionLeft, SelectionTop, SelectionWidth, SelectionHeight), Selection);
    }

    /// <summary>Draws one frame: the canvas colour, the tile grid and a selection.</summary>
    /// <param name="backend">The backend.</param>
    /// <param name="tiles">The tiles, row by row.</param>
    /// <param name="scale">How much the tiles are stretched.</param>
    /// <param name="smooth">Whether stretched tiles are sampled smoothly.</param>
    private void DrawFrame(IRenderBackend backend, IRenderSurface[] tiles, double scale, bool smooth) => DrawFrame(backend, tiles, scale, smooth, NextCanvas());

    /// <summary>Draws one frame whose tiles are a half opaque layer over the canvas colour.</summary>
    /// <param name="backend">The backend.</param>
    /// <param name="tiles">The tiles, row by row.</param>
    private void DrawFadedFrame(IRenderBackend backend, IRenderSurface[] tiles)
    {
        using var pass = backend.BeginFrame();
        pass.Clear(NextCanvas());
        pass.SetOpacity(HalfOpacity);
        for (var i = 0; i < tiles.Length; i++)
        {
            var row = Math.DivRem(i, Columns, out var column);
            pass.DrawSurface(tiles[i], new(column * TileEdge, row * TileEdge, TileEdge, TileEdge), smooth: false);
        }

        pass.SetOpacity(1);
    }

    /// <summary>
    /// Picks the canvas colour of the next frame. Two colours a shade apart take turns, so no frame has the draws of the frame
    /// before it: a backend that leaves an unchanged frame undrawn would otherwise draw nothing here.
    /// </summary>
    /// <returns>The colour.</returns>
    private uint NextCanvas()
    {
        _frames++;
        return Canvas ^ (_frames & 1);
    }
}
