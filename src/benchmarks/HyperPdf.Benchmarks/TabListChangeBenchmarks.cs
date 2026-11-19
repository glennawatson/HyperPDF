// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Tests;
using HyperPdf.ViewModels.Viewing;
using ReactiveUI.Primitives;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures what a change to the list of tabs costs the window that follows it, with hundreds of tabs open. The window follows the
/// name and the unsaved mark of every tab to keep its tab strip right, so this is the part of opening, closing or moving one tab
/// that grows with the number of tabs.
/// </summary>
public class TabListChangeBenchmarks
{
    /// <summary>The tabs open.</summary>
    private const int Tabs = 300;

    /// <summary>The application services.</summary>
    private TestServices _services = null!;

    /// <summary>The main view model.</summary>
    private MainViewModel _main = null!;

    /// <summary>The window's following of the controls.</summary>
    private IDisposable _following = null!;

    /// <summary>The changes the window was told of.</summary>
    private int _changes;

    /// <summary>Opens the tabs and follows them as a window does.</summary>
    /// <returns>A task.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _services = new();
        _main = new(_services.Services);
        var paths = new string[Tabs];
        for (var i = 0; i < Tabs; i++)
        {
            paths[i] = _services.CreateDocument($"tab-{i}.pdf", 1);
        }

        await TestServices.OpenAndWaitAsync(_main, paths);
        _following = _main.ControlChanges.SubscribeSafe(_ => _changes++, static _ => { });
    }

    /// <summary>Stops following and closes the tabs.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _following.Dispose();
        _main.Dispose();
        _services.Dispose();
    }

    /// <summary>Moves the first tab one place along, which changes the list of tabs as opening or closing one does.</summary>
    /// <returns>The changes the window was told of so far.</returns>
    [Benchmark]
    public int MoveOneTabAmongHundreds()
    {
        _main.Tabs.Move(0, 1);
        return _changes;
    }
}
