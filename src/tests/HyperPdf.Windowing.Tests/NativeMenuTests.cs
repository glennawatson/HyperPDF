// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Presentation.Input;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>Where the operating system draws the menu bar, as macOS does, the viewer hands it the menus and draws no bar of its own.</summary>
[NotInParallel]
public sealed class NativeMenuTests
{
    /// <summary>The window width in device pixels.</summary>
    private const int Width = 1100;

    /// <summary>The window height in device pixels.</summary>
    private const int Height = 700;

    /// <summary>The pages of the test document.</summary>
    private const int Pages = 3;

    /// <summary>How long one pump of the window waits, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>How long a scenario waits for a change before failing, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The place of the Document menu on the bar.</summary>
    private const int DocumentMenu = 3;

    /// <summary>The menus' names in the order they are on the bar.</summary>
    private static readonly string[] Names = ["File", "Edit", "View", "Document", "Comment", "Forms", "Tools", "Window", "Help"];

    /// <summary>The viewer hands over the nine menus once, draws no menu bar, and Alt and a letter opens no menu of its own.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NativeMenusReplaceTheBar()
    {
        var outcome = Run(static (window, session) =>
        {
            var bar = HasBar(session.Chrome);
            window.Inner.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.D, (uint)ViewerModifiers.Alt));
            _ = session.PumpOnce(PumpMilliseconds);
            return new BarOutcome(window.Titles?.ToArray() ?? [], window.Handed, bar, session.Chrome.HasOverlay, window.Application?.Quit);
        });

        await Assert.That(outcome.Titles).IsEquivalentTo(Names, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(outcome.Handed).IsEqualTo(1);
        await Assert.That(outcome.Bar).IsFalse();
        await Assert.That(outcome.MenuOpened).IsFalse();
        await Assert.That(outcome.Quit).IsEqualTo("Quit HyperPDF");
    }

    /// <summary>A native menu has the entries the viewer's own menu has: Document has Organise Pages and a Pages submenu, and File shows Ctrl+O.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NativeMenusHaveTheSameEntries()
    {
        var outcome = Run(static (window, _) =>
        {
            var document = window.Build!(DocumentMenu);
            var file = window.Build(MenuBar.FileMenu);
            var organise = Array.Find(document, static item => item.Title == "Organise Pages");
            var pages = Array.Find(document, static item => item.Title == "Pages");
            var turn = pages is null ? null : Array.Find(pages.Children, static item => item.Title == "Turn the Page Right");
            var open = Array.Exists(file, static item => item.Shortcut == "Ctrl+O");
            return new EntryOutcome(organise?.Run is not null, organise?.IsEnabled == true, turn is not null, open, Array.Exists(document, static item => item.IsSeparator));
        });

        await Assert.That(outcome.OrganiseRuns).IsTrue();
        await Assert.That(outcome.OrganiseEnabled).IsTrue();
        await Assert.That(outcome.SubmenuHasTurn).IsTrue();
        await Assert.That(outcome.OpenShowsShortcut).IsTrue();
        await Assert.That(outcome.HasSeparator).IsTrue();
    }

    /// <summary>With no document open, a menu that needs one has no entries.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MenusThatNeedADocumentAreEmptyWithoutOne()
    {
        var counts = Run(static (window, session) =>
        {
            window.Inner.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.W, (uint)ViewerModifiers.Control));
            _ = PumpUntil(session, () => session.Main.Tabs.Count == 0);
            return new EmptyOutcome(window.Build!(DocumentMenu).Length, window.Build(MenuBar.FileMenu).Length);
        });

        await Assert.That(counts.Document).IsEqualTo(0);
        await Assert.That(counts.File).IsGreaterThan(0);
    }

    /// <summary>Tells whether the viewer drew a menu bar: a row of buttons that each open a menu at a place.</summary>
    /// <param name="chrome">The window's controls.</param>
    /// <returns><see langword="true"/> when it drew one.</returns>
    private static bool HasBar(ViewerChrome chrome) =>
        chrome.Bars.Exists(static item => item is { Kind: ChromeItemKind.Button, Parameter: int, Trait: ChromeItemTrait.OpensMore });

    /// <summary>Opens a document in a window whose operating system draws the menu bar, and runs a scenario on it.</summary>
    /// <typeparam name="T">What the scenario finds.</typeparam>
    /// <param name="scenario">The scenario.</param>
    /// <returns>What the scenario found.</returns>
    private static T Run<T>(Func<NativeMenuWindow, ViewerSession, T> scenario)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-native-menu-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        try
        {
            var path = Path.Combine(folder, "menus.pdf");
            File.WriteAllBytes(path, TestPdf.Create(Pages));
            using var window = new NativeMenuWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            session.Open([path]);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true } && window.Titles is not null);
            _ = session.PumpOnce(PumpMilliseconds);
            return scenario(window, session);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Pumps the session until a condition holds or the time runs out.</summary>
    /// <param name="session">The session.</param>
    /// <param name="done">The condition.</param>
    /// <returns><see langword="true"/> when the condition held in time.</returns>
    private static bool PumpUntil(ViewerSession session, Func<bool> done)
    {
        var started = Stopwatch.GetTimestamp();
        while (!done())
        {
            if (Stopwatch.GetElapsedTime(started).TotalSeconds > TimeoutSeconds)
            {
                return false;
            }

            _ = session.PumpOnce(PumpMilliseconds);
        }

        return true;
    }

    /// <summary>What the window showed of the menus.</summary>
    /// <param name="Titles">The menus' names handed over.</param>
    /// <param name="Handed">How many times they were handed over.</param>
    /// <param name="Bar">Whether the viewer drew a menu bar.</param>
    /// <param name="MenuOpened">Whether Alt and D opened a menu of the viewer's own.</param>
    /// <param name="Quit">The words of Quit in the application menu.</param>
    private sealed record BarOutcome(string[] Titles, int Handed, bool Bar, bool MenuOpened, string? Quit);

    /// <summary>What the native menus held.</summary>
    /// <param name="OrganiseRuns">Whether Organise Pages has something to run.</param>
    /// <param name="OrganiseEnabled">Whether Organise Pages can be chosen.</param>
    /// <param name="SubmenuHasTurn">Whether the Pages submenu has Turn the Page Right.</param>
    /// <param name="OpenShowsShortcut">Whether File has an entry showing Ctrl+O.</param>
    /// <param name="HasSeparator">Whether Document has a line between groups.</param>
    private sealed record EntryOutcome(bool OrganiseRuns, bool OrganiseEnabled, bool SubmenuHasTurn, bool OpenShowsShortcut, bool HasSeparator);

    /// <summary>How many entries the menus had without a document.</summary>
    /// <param name="Document">The entries of Document.</param>
    /// <param name="File">The entries of File.</param>
    private sealed record EmptyOutcome(int Document, int File);
}
