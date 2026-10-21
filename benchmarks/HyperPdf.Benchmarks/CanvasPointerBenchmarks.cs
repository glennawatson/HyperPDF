// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Canvas;
using HyperPdf.Presentation.Input;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures routing a pointer press, move and release through the toolkit-neutral <see cref="PageCanvasState"/> on a text page,
/// the path every pointer event takes in the viewer, with no toolkit to supply the events.
/// </summary>
public class CanvasPointerBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int PageCount = 3;

    /// <summary>The text lines on each page.</summary>
    private const int TextLines = 40;

    /// <summary>The width of the viewport.</summary>
    private const double ViewWidth = 1000;

    /// <summary>The height of the viewport.</summary>
    private const double ViewHeight = 800;

    /// <summary>Where the pointer starts on the page, in points.</summary>
    private const float From = 100F;

    /// <summary>How far the pointer moves in one step, in points.</summary>
    private const float Step = 0.5F;

    /// <summary>How many pointer moves one call makes.</summary>
    private const int Moves = 100;

    /// <summary>The services of the document's tab.</summary>
    private TestServices _services = null!;

    /// <summary>The view model that owns the tab.</summary>
    private MainViewModel _main = null!;

    /// <summary>The canvas state under test.</summary>
    private PageCanvasState _state = null!;

    /// <summary>The point the moves start from, on the canvas.</summary>
    private CanvasPoint _start;

    /// <summary>Opens the document and lays it out.</summary>
    /// <returns>A task.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _services = new();
        _main = new(_services.Services);
        await TestServices.OpenAndWaitAsync(_main, [_services.CreateDocument("canvas-pointer.pdf", TestPdf.CreateTextFilled(PageCount, TextLines))]);
        var view = new CanvasSize(ViewWidth, ViewHeight);
        var host = new BenchmarkCanvasHost { Viewport = view, Extent = view };
        _state = new(host) { Tab = _main.SelectedTab! };
        _state.RebuildLayout();
        host.Extent = new(_state.Layout.ExtentWidth, _state.Layout.ExtentHeight);
        _start = _state.ToCanvas(0, new(From, From));
    }

    /// <summary>Closes the document and its services.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _main.Dispose();
        _services.Dispose();
    }

    /// <summary>Hovers over the text a hundred times with no button down, as the pointer does on the way to a click.</summary>
    /// <returns>How many moves were handled.</returns>
    [Benchmark]
    public int HoverMoves()
    {
        var handled = 0;
        for (var move = 0; move < Moves; move++)
        {
            var at = new CanvasPoint(_start.X + (move * Step), _start.Y);
            handled += _state.Move(new(at, at, false)).Handled ? 1 : 0;
        }

        return handled;
    }

    /// <summary>Presses, drags a hundred moves with the left button down, and releases, as a text selection does.</summary>
    /// <returns>How many moves were handled.</returns>
    [Benchmark]
    public int PressDragRelease()
    {
        _ = _state.Press(new(_start, _start, ViewerButtons.Left, ViewerModifiers.None, 1, false));
        var handled = 0;
        for (var move = 1; move <= Moves; move++)
        {
            var at = new CanvasPoint(_start.X + (move * Step), _start.Y);
            handled += _state.Move(new(at, at, true)).Handled ? 1 : 0;
        }

        _ = _state.Release(new(_start.X + (Moves * Step), _start.Y), false);
        _state.ClearSelection();
        return handled;
    }
}
