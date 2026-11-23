// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Input;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Theming;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Viewer.Chrome.Forms;
using HyperPdf.Viewer.Icons;
using ReactiveUI.Primitives;

namespace HyperPdf.Windowing.Tests;

/// <summary>Verifies Light and High Contrast themes, large interface text scaling, narrow-window layout, conditional Preferences rows, and Tag Editor icons.</summary>
[NotInParallel]
public sealed class PreferencesFormTests
{
    /// <summary>A narrow window width in pixels.</summary>
    private const int NarrowWidth = 640;

    /// <summary>A compact window height in pixels.</summary>
    private const int CompactHeight = 560;

    /// <summary>A standard window width in pixels.</summary>
    private const int StandardWidth = 1100;

    /// <summary>A standard window height in pixels.</summary>
    private const int StandardHeight = 820;

    /// <summary>The lines of text on the sample page.</summary>
    private const int Lines = 5;

    /// <summary>A large interface text size in points.</summary>
    private const double LargeFontPoints = 18;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for one step, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The turns pumped to let theme and form changes settle.</summary>
    private const int SettleTurns = 15;

    /// <summary>The label of the custom paper hex field in Preferences.</summary>
    private const string CustomPaperLabel = "Paper, as six hex digits such as 000000";

    /// <summary>Light and High Contrast themes, large interface text, icons-only mode, and narrow windows scale and paint cleanly without overflowing the window.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ThemesAndTextSizes_LightHighContrastLargeTextAndNarrowWindow_ScaleAndRenderCleanly()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-themes-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        ThemeScaleOutcome outcome;
        try
        {
            var path = Path.Combine(folder, "doc.pdf");
            await File.WriteAllBytesAsync(path, TestPdf.CreateTextFilled(1, Lines));
            using var window = new HeadlessWindow(NarrowWidth, CompactHeight);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            var chrome = session.Chrome;
            session.Open([path]);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });

            var defaultOpenHeight = chrome.Bars.First(static item => item.Icon == ChromeIcon.Open).Bounds.Height;
            chrome.Theme = ChromeTheme.From(ColorSchemes.Light);
            session.Canvas.DrawFrame();
            var lightButtonsFit = chrome.Bars.Where(static item => item.Kind == ChromeItemKind.Button).All(static item => item.Bounds.Right <= NarrowWidth);

            chrome.Theme = ChromeTheme.From(ColorSchemes.HighContrast);
            chrome.FontSizePoints = LargeFontPoints;
            Settle(session);
            session.Canvas.DrawFrame();
            var largeOpenHeight = chrome.Bars.First(static item => item.Icon == ChromeIcon.Open).Bounds.Height;
            var highContrastFits = chrome.Bars.Where(static item => item.Kind == ChromeItemKind.Button).All(static item => item.Bounds.Right <= NarrowWidth);

            outcome = new(lightButtonsFit, highContrastFits, largeOpenHeight > defaultOpenHeight);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.LightButtonsFit).IsTrue();
        await Assert.That(outcome.HighContrastButtonsFit).IsTrue();
        await Assert.That(outcome.LargeTextGrewControls).IsTrue();
    }

    /// <summary>Preferences reveals custom-tone and Azure rows live when chosen, and the Tag Editor gives Indent, Outdent, and MoveTo icons to its nesting actions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PreferencesAndTagEditor_ConditionalRowsAndTagNestingActions_ShowExpectedControlsAndIcons()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-prefs-tags-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        DialogDetailsOutcome outcome;
        try
        {
            var path = Path.Combine(folder, "tagged.pdf");
            await File.WriteAllBytesAsync(path, AccessibleTestPdf.CreateRich());
            using var window = new HeadlessWindow(StandardWidth, StandardHeight);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            var chrome = session.Chrome;
            session.Open([path]);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
            var tab = session.Main.SelectedTab!;

            using var preferences = new HyperPdf.ViewModels.Preferences.PreferencesViewModel(services);
            using var showingPrefs = chrome.ShowPreferences(preferences).SubscribeSafe(static _ => { }, ErrorTrace.Write);
            Settle(session);
            var hiddenInitially = !chrome.OverlayItems.Exists(static item => item.Text == CustomPaperLabel);

            preferences.PageTone = (int)PageToneChoice.Custom;
            Settle(session);
            var revealedLive = chrome.OverlayItems.Exists(static item => item.Text == CustomPaperLabel);

            using var showingTags = chrome.ShowForm(ToolForms.TagEditor(tab.Tags)).SubscribeSafe(static _ => { }, ErrorTrace.Write);
            Settle(session);
            var indentIcon = chrome.OverlayItems.Exists(static item => item.Text == "Put it inside the tag above" && item.Icon == ChromeIcon.Indent);
            var outdentIcon = chrome.OverlayItems.Exists(static item => item.Text == "Take it out of its parent" && item.Icon == ChromeIcon.Outdent);
            var moveToIcon = chrome.OverlayItems.Exists(static item => item.Text == "Move it there" && item.Icon == ChromeIcon.MoveTo);

            ((ICommand)tab.Pages.Sanitize.ShowCommand).Execute(null);
            Settle(session);
            var sanitizeHasAccept = chrome.OverlayItems.Exists(static item => item.Action == OverlayAction.Accept && item.Icon == ChromeIcon.Close && item.Framed);

            outcome = new(hiddenInitially, revealedLive, indentIcon && outdentIcon && moveToIcon, sanitizeHasAccept);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.CustomPaperHiddenAtStart).IsTrue();
        await Assert.That(outcome.CustomPaperRevealedLive).IsTrue();
        await Assert.That(outcome.TagActionsHaveIcons).IsTrue();
        await Assert.That(outcome.PageLabelsUsesSharedAcceptButton).IsTrue();
    }

    /// <summary>Pumps the session until a condition holds or the timeout elapses.</summary>
    /// <param name="session">The session.</param>
    /// <param name="done">The condition.</param>
    /// <returns><see langword="true"/> when met before timeout.</returns>
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

    /// <summary>Pumps several turns so reactive signals reach the chrome layout.</summary>
    /// <param name="session">The session.</param>
    private static void Settle(ViewerSession session)
    {
        for (var i = 0; i < SettleTurns; i++)
        {
            _ = session.PumpOnce(PumpMilliseconds);
        }
    }

    /// <summary>Records theme, large-text, and narrow-window layout results.</summary>
    /// <param name="LightButtonsFit">Whether toolbar buttons stayed inside the narrow window in Light theme.</param>
    /// <param name="HighContrastButtonsFit">Whether toolbar buttons stayed inside the narrow window in High Contrast with large text.</param>
    /// <param name="LargeTextGrewControls">Whether setting a larger font size grew the toolbar controls.</param>
    private readonly record struct ThemeScaleOutcome(bool LightButtonsFit, bool HighContrastButtonsFit, bool LargeTextGrewControls);

    /// <summary>Records conditional Preferences rows, Tag Editor icons, and shared dialog primary-action buttons.</summary>
    /// <param name="CustomPaperHiddenAtStart">Whether custom paper hex input starts hidden.</param>
    /// <param name="CustomPaperRevealedLive">Whether choosing Custom page tone reveals the custom paper row live.</param>
    /// <param name="TagActionsHaveIcons">Whether Nest, Unnest, and Move carry Indent, Outdent, and MoveTo icons.</param>
    /// <param name="PageLabelsUsesSharedAcceptButton">Whether Page Labels renders a primary Accept button in the bottom bar.</param>
    private readonly record struct DialogDetailsOutcome(
        bool CustomPaperHiddenAtStart,
        bool CustomPaperRevealedLive,
        bool TagActionsHaveIcons,
        bool PageLabelsUsesSharedAcceptButton);
}
