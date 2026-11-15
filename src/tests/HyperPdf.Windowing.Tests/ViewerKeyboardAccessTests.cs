// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Presentation.Input;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Accessibility;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Windowing.Windows;
using ReactiveUI.Primitives;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// The viewer can be used without a pointer: keys reach every control and tool, the tree tells a screen reader where the keyboard
/// is and what a control is, form fields on the page are in the tree and can be pressed, and messages are spoken.
/// </summary>
[NotInParallel]
public sealed class ViewerKeyboardAccessTests
{
    /// <summary>The window width in device pixels.</summary>
    private const int Width = 1100;

    /// <summary>The window height in device pixels.</summary>
    private const int Height = 700;

    /// <summary>The pages of the text document.</summary>
    private const int Pages = 3;

    /// <summary>The lines on each page of the text document.</summary>
    private const int Lines = 20;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest a step may take, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>How long an element must stay in place before it counts as settled, in seconds.</summary>
    private const double StillSeconds = 0.5;

    /// <summary>The settings file name.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>The file name of the document of plain text.</summary>
    private const string TextFile = "text.pdf";

    /// <summary>The message the announcement test sends.</summary>
    private const string Spoken = "Three matches";

    /// <summary>F6 puts the keyboard on the controls, Tab moves it, Enter presses, and Escape gives the keyboard back to the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeysMoveBetweenThePageAndTheControls()
    {
        var outcome = Run(TextFile, TestPdf.CreateTextFilled(Pages, Lines), static (window, session) =>
        {
            var chrome = session.Chrome;
            _ = session.Accessibility.Get(session.Accessibility.RootId);
            Key(window, ViewerKey.F6, ViewerModifiers.None);
            var onControls = PumpUntil(session, () => chrome.FocusedControl >= 0);
            var first = chrome.FocusedControl;
            var readerFollows = PumpUntil(session, () => session.Accessibility.FocusedId == ViewerNodes.FirstControlId + chrome.FocusedControl);
            Key(window, ViewerKey.Tab, ViewerModifiers.None);
            var moved = PumpUntil(session, () => chrome.FocusedControl != first);

            // The next page button is reached by Tab alone and pressed with Enter.
            var next = IndexOf(chrome.Bars, ChromeCommand.NextPage);
            for (var tries = 0; tries < chrome.Bars.Count && chrome.FocusedControl != next; tries++)
            {
                Key(window, ViewerKey.Tab, ViewerModifiers.None);
                _ = session.PumpOnce(PumpMilliseconds);
            }

            Key(window, ViewerKey.Enter, ViewerModifiers.None);
            var pressed = PumpUntil(session, () => session.Main.SelectedTab is { CurrentPageIndex: > 0 });
            Key(window, ViewerKey.Escape, ViewerModifiers.None);
            var back = PumpUntil(session, () => chrome.FocusedControl < 0);
            return new KeyboardOutcome(onControls, readerFollows, moved, pressed, back);
        });

        await Assert.That(outcome.OnControls).IsTrue();
        await Assert.That(outcome.ReaderFollows).IsTrue();
        await Assert.That(outcome.Moved).IsTrue();
        await Assert.That(outcome.Pressed).IsTrue();
        await Assert.That(outcome.BackOnPage).IsTrue();
    }

    /// <summary>A shortcut opens the preferences, Tab moves through its rows, and a switch is flipped with Space and reported to a screen reader as a tick box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DialogRowsWorkByKeyboardAndReportTheirState()
    {
        var outcome = Run(TextFile, TestPdf.CreateTextFilled(Pages, Lines), static (window, session) =>
        {
            var chrome = session.Chrome;
            _ = session.Accessibility.Get(session.Accessibility.RootId);
            Key(window, ViewerKey.Comma, ViewerModifiers.Control);
            var opened = PumpUntil(session, () => chrome.HasOverlay);

            // Tab goes down the rows until it reaches the first switch that is in view.
            var reached = false;
            for (var tries = 0; tries < chrome.OverlayItems.Count + chrome.OverlayItems.Count && !reached; tries++)
            {
                Key(window, ViewerKey.Tab, ViewerModifiers.None);
                _ = session.PumpOnce(PumpMilliseconds);
                reached = chrome.FocusIsInOverlay && Focused(chrome) is { Trait: ChromeItemTrait.Switch };
            }

            var before = Focused(chrome)?.Selected ?? false;
            var id = ViewerNodes.FirstOverlayId + chrome.FocusedControl;
            _ = PumpUntil(session, () => session.Accessibility.Get(id) is { Role: AccessibleRole.CheckBox });
            var role = session.Accessibility.Get(id)?.Role ?? AccessibleRole.Unknown;
            Key(window, ViewerKey.Space, ViewerModifiers.None);
            var flipped = PumpUntil(session, () => Focused(chrome) is { Trait: ChromeItemTrait.Switch } now && now.Selected != before);
            var ticked = PumpUntil(session, () => session.Accessibility.Get(ViewerNodes.FirstOverlayId + chrome.FocusedControl) is { } node
                && ((node.States & AccessibleStates.Checked) != 0) == !before);

            // Flip it back, so the settings are left as they were.
            Key(window, ViewerKey.Space, ViewerModifiers.None);
            _ = PumpUntil(session, () => Focused(chrome)?.Selected == before);
            Key(window, ViewerKey.Escape, ViewerModifiers.None);
            var closed = PumpUntil(session, () => !chrome.HasOverlay);
            return new DialogOutcome(opened, reached, role, flipped, ticked, closed);
        });

        await Assert.That(outcome.Opened).IsTrue();
        await Assert.That(outcome.ReachedSwitch).IsTrue();
        await Assert.That(outcome.Role).IsEqualTo(AccessibleRole.CheckBox);
        await Assert.That(outcome.Flipped).IsTrue();
        await Assert.That(outcome.TickReported).IsTrue();
        await Assert.That(outcome.Closed).IsTrue();
    }

    /// <summary>The fields of a form are elements of the document, and pressing one through the tree starts filling it in.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormFieldsAreInTheTreeAndCanBePressed()
    {
        var outcome = Run("form.pdf", TestPdf.CreateForm(), static (window, session) =>
        {
            _ = session.Accessibility.Get(session.Accessibility.RootId);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true, Forms.HasForm: true });
            PageSettling.Wait(session);
            var found = PumpUntil(session, () => FirstPageItem(session) >= 0);
            PumpUntilStill(session);
            var id = FirstPageItem(session);
            var node = session.Accessibility.Get(id);
            var acted = session.Accessibility.DoAction(id, 0);
            var pressed = acted
                && PumpUntil(session, () => session.Main.SelectedTab is { } tab && (tab.Forms.Editing is not null || tab.Forms.Focused is not null));
            var forms = session.Main.SelectedTab?.Forms;
            var detail = $"item {id}, role {node?.Role}, name \"{node?.Name}\", acted {acted}, editing {forms?.Editing?.Name}, focused {forms?.Focused?.Name}, "
                + $"loaded {session.Main.SelectedTab?.IsLoaded}";
            return new FieldOutcome(found, node?.Name.Length > 0 || node?.Description.Length > 0, pressed, detail);
        });

        await Assert.That(outcome.Found).IsTrue();
        await Assert.That(outcome.Described).IsTrue();
        await Assert.That(outcome.Pressed).IsTrue().Because(outcome.Detail);
    }

    /// <summary>A tool that is running shows a bar with what it says and a button that ends it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunningToolShowsABarThatEndsIt()
    {
        var outcome = Run(TextFile, TestPdf.CreateTextFilled(Pages, Lines), static (window, session) =>
        {
            var chrome = session.Chrome;
            if (session.Main.SelectedTab is not { } tab)
            {
                return default;
            }

            var none = !chrome.Bars.Any(static item => item.Command == ChromeCommand.ToolAction);
            ((System.Windows.Input.ICommand)tab.Measure.ToggleCommand).Execute(null);
            var shown = PumpUntil(session, () => chrome.Bars.Any(static item => item.Command == ChromeCommand.ToolAction));

            // The last button of the measuring bar is the one that ends measuring.
            var done = -1;
            for (var i = 0; i < chrome.Bars.Count; i++)
            {
                done = chrome.Bars[i].Command == ChromeCommand.ToolAction ? i : done;
            }

            var pressed = chrome.PressControl(false, done);
            var ended = PumpUntil(session, () => !tab.Measure.IsOn && !chrome.Bars.Any(static item => item.Command == ChromeCommand.ToolAction));
            return new ToolOutcome(none, shown, pressed, ended);
        });

        await Assert.That(outcome.NoneAtFirst).IsTrue();
        await Assert.That(outcome.Shown).IsTrue();
        await Assert.That(outcome.Pressed).IsTrue();
        await Assert.That(outcome.Ended).IsTrue();
    }

    /// <summary>The focus mode shortcut shows the document's text as a column in place of the pages, and Escape goes back to the pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FocusModeShowsTheTextAndEscapeLeavesIt()
    {
        var outcome = Run(TextFile, TestPdf.CreateTextFilled(Pages, Lines), static (window, session) =>
        {
            var chrome = session.Chrome;
            Key(window, ViewerKey.Digit4, ViewerModifiers.Control);
            var opened = PumpUntil(session, () => chrome.HasOverlay && session.Main.SelectedTab is { FocusMode.IsOn: true });
            var text = PumpUntil(session, () => chrome.OverlayItems.Any(static item => item is { Command: ChromeCommand.PickRow, Text.Length: > 0 }));
            var larger = chrome.OverlayItems.Any(static item => item.TextSize > 0);
            Key(window, ViewerKey.Escape, ViewerModifiers.None);
            var left = PumpUntil(session, () => !chrome.HasOverlay && session.Main.SelectedTab is { FocusMode.IsOn: false });
            return new FocusOutcome(opened, text, larger, left);
        });

        await Assert.That(outcome.Opened).IsTrue();
        await Assert.That(outcome.TextShown).IsTrue();
        await Assert.That(outcome.OwnTextSize).IsTrue();
        await Assert.That(outcome.Left).IsTrue();
    }

    /// <summary>Closing the last document shows the start view, which comes back after the menu opened over it closes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StartViewStandsInWhenNoDocumentIsOpen()
    {
        var outcome = Run(TextFile, TestPdf.CreateTextFilled(Pages, Lines), static (window, session) =>
        {
            var chrome = session.Chrome;
            var hidden = !chrome.HasOverlay;
            Key(window, ViewerKey.W, ViewerModifiers.Control);
            var shown = PumpUntil(session, () => session.Main.Tabs.Count == 0 && chrome.OverlayItems.Any(static item => item is { Kind: ChromeItemKind.Button, Command: ChromeCommand.PickRow }));
            Key(window, ViewerKey.F10, ViewerModifiers.None);
            var menu = PumpUntil(session, () => chrome.OverlayItems.Any(static item => item.Command == ChromeCommand.MenuEntry));
            Key(window, ViewerKey.Escape, ViewerModifiers.None);
            var back = PumpUntil(session, () => chrome.OverlayItems.Any(static item => item is { Kind: ChromeItemKind.Button, Command: ChromeCommand.PickRow }));
            return new StartOutcome(hidden, shown, menu, back);
        });

        await Assert.That(outcome.HiddenWithADocument).IsTrue();
        await Assert.That(outcome.Shown).IsTrue();
        await Assert.That(outcome.MenuOpened).IsTrue();
        await Assert.That(outcome.CameBack).IsTrue();
    }

    /// <summary>A message is sent to a screen reader as an announcement, once one is reading.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MessagesAreAnnounced()
    {
        var heard = Run(TextFile, TestPdf.CreateTextFilled(Pages, Lines), static (window, session) =>
        {
            var messages = new List<string>();
            using var listening = session.Accessibility.Changes
                .Where(static change => change.Kind == AccessibleChangeKind.Announcement)
                .Subscribe(change => messages.Add(change.Message ?? string.Empty));
            _ = session.Accessibility.Get(session.Accessibility.RootId);
            _ = session.PumpOnce(PumpMilliseconds);
            session.Accessibility.Announce(Spoken);
            return messages.Count == 1 ? messages[0] : string.Empty;
        });

        await Assert.That(heard).IsEqualTo(Spoken);
    }

    /// <summary>Opens a document in a headless viewer and runs a scenario against it on the window thread.</summary>
    /// <typeparam name="T">What the scenario reports.</typeparam>
    /// <param name="name">The file name of the document.</param>
    /// <param name="pdf">The document.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>What the scenario reported.</returns>
    private static T Run<T>(string name, byte[] pdf, Func<HeadlessWindow, ViewerSession, T> scenario)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-keys-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        try
        {
            var path = Path.Combine(folder, name);
            File.WriteAllBytes(path, pdf);
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

    /// <summary>Gets the control of the open dialog the keyboard is on.</summary>
    /// <param name="chrome">The window's controls.</param>
    /// <returns>The control, or <see langword="null"/>.</returns>
    private static ChromeItem? Focused(ViewerChrome chrome) =>
        chrome.FocusIsInOverlay && (uint)chrome.FocusedControl < (uint)chrome.OverlayItems.Count ? chrome.OverlayItems[chrome.FocusedControl] : null;

    /// <summary>Finds the first form field, link or note of the page in the tree.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The element's number, or -1 when the document has none yet.</returns>
    private static int FirstPageItem(ViewerSession session)
    {
        if (session.Accessibility.Get(ViewerNodes.DocumentId) is not { } document)
        {
            return -1;
        }

        foreach (var child in document.Children)
        {
            if (child >= ViewerNodes.FirstPageItemId)
            {
                return child;
            }
        }

        return -1;
    }

    /// <summary>Pumps until the first element of the page has stopped moving. The page settles to the window after it loads, and a press lands where the element was.</summary>
    /// <param name="session">The session.</param>
    private static void PumpUntilStill(ViewerSession session)
    {
        var last = session.Accessibility.Get(FirstPageItem(session))?.Bounds;
        var since = Stopwatch.GetTimestamp();
        _ = PumpUntil(
            session,
            () =>
            {
                var now = session.Accessibility.Get(FirstPageItem(session))?.Bounds;
                if (!Equals(now, last))
                {
                    last = now;
                    since = Stopwatch.GetTimestamp();
                }

                return Stopwatch.GetElapsedTime(since).TotalSeconds >= StillSeconds;
            });
    }

    /// <summary>Finds the first control that does one thing.</summary>
    /// <param name="items">The controls.</param>
    /// <param name="command">What the control does.</param>
    /// <returns>Its place in the list, or -1.</returns>
    private static int IndexOf(IReadOnlyList<ChromeItem> items, ChromeCommand command)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Command == command)
            {
                return i;
            }
        }

        return -1;
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

    /// <summary>What moving the keyboard round the window did.</summary>
    /// <param name="OnControls">Whether F6 put the keyboard on a control.</param>
    /// <param name="ReaderFollows">Whether the tree reported that control as the one with the keyboard.</param>
    /// <param name="Moved">Whether Tab moved to another control.</param>
    /// <param name="Pressed">Whether Enter pressed the control the keyboard was on.</param>
    /// <param name="BackOnPage">Whether Escape gave the keyboard back to the page.</param>
    private readonly record struct KeyboardOutcome(bool OnControls, bool ReaderFollows, bool Moved, bool Pressed, bool BackOnPage);

    /// <summary>What using the preferences by keyboard did.</summary>
    /// <param name="Opened">Whether the shortcut opened the window.</param>
    /// <param name="ReachedSwitch">Whether Tab reached a switch.</param>
    /// <param name="Role">The role the tree gave the switch.</param>
    /// <param name="Flipped">Whether Space flipped the switch.</param>
    /// <param name="TickReported">Whether the tree reported the new state.</param>
    /// <param name="Closed">Whether Escape closed the window.</param>
    private readonly record struct DialogOutcome(bool Opened, bool ReachedSwitch, AccessibleRole Role, bool Flipped, bool TickReported, bool Closed);

    /// <summary>What the bar of a running tool did.</summary>
    /// <param name="NoneAtFirst">Whether there was no tool bar before the tool ran.</param>
    /// <param name="Shown">Whether the bar appeared when the tool started.</param>
    /// <param name="Pressed">Whether its last button could be pressed.</param>
    /// <param name="Ended">Whether the press ended the tool and took the bar away.</param>
    private readonly record struct ToolOutcome(bool NoneAtFirst, bool Shown, bool Pressed, bool Ended);

    /// <summary>What focus mode did.</summary>
    /// <param name="Opened">Whether the shortcut turned focus mode on.</param>
    /// <param name="TextShown">Whether the document's text arrived as paragraphs that can be pressed.</param>
    /// <param name="OwnTextSize">Whether the text is drawn at focus mode's own size.</param>
    /// <param name="Left">Whether Escape went back to the pages.</param>
    private readonly record struct FocusOutcome(bool Opened, bool TextShown, bool OwnTextSize, bool Left);

    /// <summary>What the start view did.</summary>
    /// <param name="HiddenWithADocument">Whether nothing lay over the pages while a document was open.</param>
    /// <param name="Shown">Whether closing the last document showed the start view.</param>
    /// <param name="MenuOpened">Whether F10 opened the main menu over it.</param>
    /// <param name="CameBack">Whether the start view came back when the menu closed.</param>
    private readonly record struct StartOutcome(bool HiddenWithADocument, bool Shown, bool MenuOpened, bool CameBack);

    /// <summary>What the tree showed of a form.</summary>
    /// <param name="Found">Whether a field of the form was an element of the document.</param>
    /// <param name="Described">Whether the element had a name or a description to read.</param>
    /// <param name="Pressed">Whether pressing it through the tree reached the form.</param>
    /// <param name="Detail">What the element was, for the message of a failure.</param>
    private readonly record struct FieldOutcome(bool Found, bool Described, bool Pressed, string Detail);
}
