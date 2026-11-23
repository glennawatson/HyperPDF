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

/// <summary>
/// The menu bar under the tabs lists File, Edit, View, Document, Comment, Forms, Tools, Window and Help. Alt and a menu's letter
/// opens it, F10 opens File, and Left and Right move to the menu beside the open one.
/// </summary>
[NotInParallel]
public sealed class MenuBarTests
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

    /// <summary>The settings file of a scenario.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>The test document's file name.</summary>
    private const string FileName = "menus.pdf";

    /// <summary>The place of the Edit menu on the bar.</summary>
    private const int EditMenu = 1;

    /// <summary>The place of the Document menu on the bar.</summary>
    private const int DocumentMenu = 3;

    /// <summary>The place of the Forms menu on the bar.</summary>
    private const int FormsMenu = 5;

    /// <summary>The place of the Window menu on the bar.</summary>
    private const int WindowMenu = 7;

    /// <summary>The menus' names in the order they are on the bar.</summary>
    private static readonly string[] Names = ["File", "Edit", "View", "Document", "Comment", "Forms", "Tools", "Window", "Help"];

    /// <summary>The bar is a row of the nine menus' names under the tabs and over the toolbar, and each can open with a document open.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BarListsTheNineMenusInOrder()
    {
        var bar = Run(static (_, session) =>
        {
            // The bars are laid out again on the next frame after the document has loaded.
            var settled = PumpUntil(session, () => MenuNames(session.Chrome) is { AllEnabled: true, Names.Length: > 0 });
            return settled ? MenuNames(session.Chrome) : new([], false, false);
        });

        await Assert.That(bar.Names).IsEquivalentTo(Names, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(bar.AllEnabled).IsTrue();
        await Assert.That(bar.OnOneLine).IsTrue();
    }

    /// <summary>Alt and D opens Document. Right moves to Comment, Left twice goes back past Document to View, and Escape closes it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AltLetterOpensAMenuAndArrowsMoveAlongTheBar()
    {
        var outcome = Run(static (window, session) =>
        {
            var chrome = session.Chrome;
            Key(window, ViewerKey.D, ViewerModifiers.Alt);
            var document = PumpUntil(session, () => HasRow(chrome, "Organise Pages") && HasRow(chrome, "Compare with Another Document…"));
            Key(window, ViewerKey.Right, ViewerModifiers.None);
            var comment = PumpUntil(session, () => HasRow(chrome, "Highlight") && HasRow(chrome, "More Marking Tools"));
            Key(window, ViewerKey.Left, ViewerModifiers.None);
            Key(window, ViewerKey.Left, ViewerModifiers.None);
            var view = PumpUntil(session, () => HasRow(chrome, "Zoom In") && HasRow(chrome, "Reading Aids"));
            Key(window, ViewerKey.Escape, ViewerModifiers.None);
            var closed = PumpUntil(session, () => !chrome.HasOverlay);
            return new MoveOutcome(document, comment, view, closed);
        });

        await Assert.That(outcome.Document).IsTrue();
        await Assert.That(outcome.Comment).IsTrue();
        await Assert.That(outcome.View).IsTrue();
        await Assert.That(outcome.Closed).IsTrue();
    }

    /// <summary>Right on an entry that opens a submenu opens it, and does not move to the next menu.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightOpensASubmenu()
    {
        var opened = Run(static (window, session) =>
        {
            var chrome = session.Chrome;
            Key(window, ViewerKey.D, ViewerModifiers.Alt);
            _ = PumpUntil(session, () => HasRow(chrome, "Organise Pages"));

            // Organise Pages is first, then a line, then Pages, which opens a submenu.
            Key(window, ViewerKey.Down, ViewerModifiers.None);
            Key(window, ViewerKey.Down, ViewerModifiers.None);
            Key(window, ViewerKey.Right, ViewerModifiers.None);
            return PumpUntil(session, () => HasRow(chrome, "Back") && HasRow(chrome, "Turn the Page Right"));
        });

        await Assert.That(opened).IsTrue();
    }

    /// <summary>With no document open, only File, Edit, Window and Help can open, and moving along the bar passes over the rest.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WithoutADocumentOnlyTheFileMenusOpen()
    {
        var outcome = Run(static (window, session) =>
        {
            Key(window, ViewerKey.W, ViewerModifiers.Control);
            _ = PumpUntil(session, () => session.Main.Tabs.Count == 0);
            var main = session.Main;
            var documentOpens = MenuBar.CanOpen(main, DocumentMenu);
            var editOpens = MenuBar.CanOpen(main, EditMenu);
            var besideEdit = MenuBar.Beside(main, EditMenu, 1);
            var beforeWindow = MenuBar.Beside(main, WindowMenu, -1);
            return new EmptyOutcome(documentOpens, editOpens, besideEdit, beforeWindow);
        });

        await Assert.That(outcome.DocumentOpens).IsFalse();
        await Assert.That(outcome.EditOpens).IsTrue();
        await Assert.That(outcome.BesideEdit).IsEqualTo(WindowMenu);
        await Assert.That(outcome.BeforeWindow).IsEqualTo(EditMenu);
    }

    /// <summary>Each menu's letter in English opens it, and a letter no menu uses opens none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LettersFindTheirMenus()
    {
        await Assert.That(MenuBar.ForKey(ViewerKey.F)).IsEqualTo(MenuBar.FileMenu);
        await Assert.That(MenuBar.ForKey(ViewerKey.D)).IsEqualTo(DocumentMenu);
        await Assert.That(MenuBar.ForKey(ViewerKey.O)).IsEqualTo(FormsMenu);
        await Assert.That(MenuBar.ForKey(ViewerKey.Z)).IsEqualTo(-1);
        await Assert.That(MenuBar.ForKey(ViewerKey.F10)).IsEqualTo(-1);
    }

    /// <summary>Reads the names on the bar, whether each can open and whether they share one line.</summary>
    /// <param name="chrome">The window's controls.</param>
    /// <returns>What the bar shows.</returns>
    private static BarOutcome MenuNames(ViewerChrome chrome)
    {
        var names = new List<string>();
        var enabled = true;
        var top = -1.0;
        var oneLine = true;
        foreach (var item in chrome.Bars)
        {
            if (item is not { Kind: ChromeItemKind.Button, Parameter: int })
            {
                continue;
            }

            names.Add(item.Text);
            enabled &= item.Enabled;
            oneLine &= top < 0 || Math.Abs(item.Bounds.Y - top) < 1;
            top = item.Bounds.Y;
        }

        return new([.. names], enabled, oneLine);
    }

    /// <summary>Tells whether the open menu has a row with some words.</summary>
    /// <param name="chrome">The window's controls.</param>
    /// <param name="text">The words.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool HasRow(ViewerChrome chrome, string text) =>
        chrome.OverlayItems.Exists(item => item.Kind == ChromeItemKind.Row && item.Text == text);

    /// <summary>Opens a document in a window with no screen and runs a scenario on it.</summary>
    /// <typeparam name="T">What the scenario finds.</typeparam>
    /// <param name="scenario">The scenario.</param>
    /// <returns>What the scenario found.</returns>
    private static T Run<T>(Func<HeadlessWindow, ViewerSession, T> scenario)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-menubar-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        try
        {
            var path = Path.Combine(folder, FileName);
            File.WriteAllBytes(path, TestPdf.Create(Pages));
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            session.Open([path]);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
            return scenario(window, session);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Sends a key press to the window.</summary>
    /// <param name="window">The window.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    private static void Key(HeadlessWindow window, ViewerKey key, ViewerModifiers modifiers) =>
        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)key, (uint)modifiers));

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

    /// <summary>What the bar shows.</summary>
    /// <param name="Names">The menus' names, left to right.</param>
    /// <param name="AllEnabled">Whether every menu can open.</param>
    /// <param name="OnOneLine">Whether every name is on one line.</param>
    private readonly record struct BarOutcome(string[] Names, bool AllEnabled, bool OnOneLine);

    /// <summary>What moving along the bar showed.</summary>
    /// <param name="Document">Whether Alt and D opened Document.</param>
    /// <param name="Comment">Whether Right then showed Comment.</param>
    /// <param name="View">Whether Left twice then showed View.</param>
    /// <param name="Closed">Whether Escape closed the menu.</param>
    private readonly record struct MoveOutcome(bool Document, bool Comment, bool View, bool Closed);

    /// <summary>What the bar allows with no document open.</summary>
    /// <param name="DocumentOpens">Whether Document can open.</param>
    /// <param name="EditOpens">Whether Edit can open.</param>
    /// <param name="BesideEdit">The menu Right goes to from Edit.</param>
    /// <param name="BeforeWindow">The menu Left goes to from Window.</param>
    private readonly record struct EmptyOutcome(bool DocumentOpens, bool EditOpens, int BesideEdit, int BeforeWindow);
}
