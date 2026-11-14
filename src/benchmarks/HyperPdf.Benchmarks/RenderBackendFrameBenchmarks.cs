// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Cpu;
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

    /// <summary>The left edge of the selection.</summary>
    private const double SelectionLeft = 200;

    /// <summary>The top edge of the selection.</summary>
    private const double SelectionTop = 300;

    /// <summary>The width of the selection.</summary>
    private const double SelectionWidth = 900;

    /// <summary>The height of the selection.</summary>
    private const double SelectionHeight = 24;

    /// <summary>The CPU backend.</summary>
    private CpuRenderBackend _cpu = null!;

    /// <summary>The CPU tiles.</summary>
    private IRenderSurface[] _cpuTiles = [];

    /// <summary>The Vulkan backend, or <see langword="null"/> on a machine with no Vulkan driver.</summary>
    private VulkanRenderBackend? _gpu;

    /// <summary>The Vulkan tiles.</summary>
    private IRenderSurface[] _gpuTiles = [];

    /// <summary>Creates both backends and their filled tiles, and draws one frame so tiles are uploaded.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _cpu = new(static (_, _, _) => { });
        _cpu.Resize(new(FrameWidth, FrameHeight));
        _cpuTiles = CreateTiles(_cpu);
        if (VulkanContext.TryCreate() is not { } context)
        {
            return;
        }

        _gpu = new(context);
        _gpu.Resize(new(FrameWidth, FrameHeight));
        _gpuTiles = CreateTiles(_gpu);
        DrawFrame(_gpu, _gpuTiles, 1, smooth: false);
    }

    /// <summary>Releases the tiles and the backends.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        DisposeTiles(_cpuTiles);
        DisposeTiles(_gpuTiles);
        _cpu.Dispose();
        _gpu?.Dispose();
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

    /// <summary>Draws one frame: the canvas colour, the tile grid and a selection.</summary>
    /// <param name="backend">The backend.</param>
    /// <param name="tiles">The tiles, row by row.</param>
    /// <param name="scale">How much the tiles are stretched.</param>
    /// <param name="smooth">Whether stretched tiles are sampled smoothly.</param>
    private static void DrawFrame(IRenderBackend backend, IRenderSurface[] tiles, double scale, bool smooth)
    {
        using var pass = backend.BeginFrame();
        pass.Clear(Canvas);
        var edge = TileEdge * scale;
        for (var i = 0; i < tiles.Length; i++)
        {
            var row = Math.DivRem(i, Columns, out var column);
            pass.DrawSurface(tiles[i], new(column * edge, row * edge, edge, edge), smooth);
        }

        pass.FillRect(new(SelectionLeft, SelectionTop, SelectionWidth, SelectionHeight), Selection);
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

    /// <summary>Disposes every tile.</summary>
    /// <param name="tiles">The tiles.</param>
    private static void DisposeTiles(IRenderSurface[] tiles)
    {
        foreach (var tile in tiles)
        {
            tile.Dispose();
        }
    }
}
