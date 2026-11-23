// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer;
using HyperPdf.Windowing.Tests;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures building the right-click menus: on a page, on a link, on selected text, on a page in the side panel and on a
/// bookmark. Each builds the entries a right press shows, from an open document.
/// </summary>
public class ContextMenuBuildBenchmarks
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 1280;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 800;

    /// <summary>The pages in the document, which has a bookmark on each.</summary>
    private const int Pages = 20;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The page point the menus are opened at.</summary>
    private static readonly PagePoint At = new(200, 300);

    /// <summary>The selected lines on the first page.</summary>
    private static readonly Dictionary<int, List<PageRect>> Lines = new() { [0] = [new(72, 100, 300, 14), new(72, 120, 280, 14)] };

    /// <summary>A link to another page of the document.</summary>
    private static readonly PageLink Link = new(new(72, 100, 120, 14), new(LinkTargetKind.Page, 1, null, null));

    /// <summary>A page in the side panel.</summary>
    private static readonly ThumbnailItemViewModel Thumbnail = new(1, "2", 120, 160);

    /// <summary>The folder for the settings and the document.</summary>
    private string _folder = string.Empty;

    /// <summary>The synchronisation context before the benchmark took the thread.</summary>
    private SynchronizationContext? _before;

    /// <summary>The window.</summary>
    private HeadlessWindow _window = null!;

    /// <summary>The application services.</summary>
    private AppServices _services = null!;

    /// <summary>The viewer session.</summary>
    private ViewerSession _session = null!;

    /// <summary>The tab the menus are for.</summary>
    private DocumentTabViewModel _tab = null!;

    /// <summary>The command that copies text, as the page's selection menu is given.</summary>
    private ReactiveCommand<string, RxVoid> _copy = null!;

    /// <summary>The first bookmark of the document.</summary>
    private OutlineItemViewModel _bookmark = null!;

    /// <summary>Opens a document with bookmarks and builds each menu once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-context-menu-benchmark-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "menus.pdf");
        File.WriteAllBytes(path, TestPdf.Create(Pages));
        _before = SynchronizationContext.Current;
        _window = new(Width, Height);
        var context = ViewerSession.UseWindowThread(_window);
        _services = new(new SettingsStore(Path.Combine(_folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(_window));
        _session = new(_window, _services, context);
        _session.Open([path]);
        while (_session.Main.SelectedTab is not { IsLoaded: true } || _session.Main.SelectedTab.Outline.Length == 0)
        {
            _ = _session.PumpOnce(PumpMilliseconds);
        }

        _tab = _session.Main.SelectedTab;
        _bookmark = _tab.Outline[0];
        _copy = ReactiveCommand.Create<string>(static _ => { });
        _ = PageMenu();
        _ = LinkMenu();
        _ = SelectionMenu();
        _ = ThumbnailMenu();
        _ = BookmarkMenu();
    }

    /// <summary>Closes the viewer and removes its files.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _copy.Dispose();
        _session.Dispose();
        _services.Dispose();
        _window.Dispose();
        SynchronizationContext.SetSynchronizationContext(_before);
        Directory.Delete(_folder, true);
    }

    /// <summary>Builds the menu of a right press on a page.</summary>
    /// <returns>The number of entries.</returns>
    [Benchmark(Baseline = true)]
    public int PageMenu() => ContextMenus.ForPage(_tab, new(0, At)).Count;

    /// <summary>Builds the menu of a right press on a link.</summary>
    /// <returns>The number of entries.</returns>
    [Benchmark]
    public int LinkMenu() => ContextMenus.ForLink(_tab, Link, new(0, At), (ICommand)_copy).Count;

    /// <summary>Builds the menu of a right press on selected text.</summary>
    /// <returns>The number of entries.</returns>
    [Benchmark]
    public int SelectionMenu() => ContextMenus.ForSelection(_tab, (ICommand)_copy, "The quick brown fox", Lines, default).Count;

    /// <summary>Builds the menu of a right press on a page in the side panel.</summary>
    /// <returns>The number of entries.</returns>
    [Benchmark]
    public int ThumbnailMenu() => SidebarContextMenus.ForRow(_tab, Thumbnail)!.Count;

    /// <summary>Builds the menu of a right press on a bookmark.</summary>
    /// <returns>The number of entries.</returns>
    [Benchmark]
    public int BookmarkMenu() => SidebarContextMenus.ForRow(_tab, _bookmark)!.Count;
}
