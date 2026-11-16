// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;
using HyperPdf.Core.Reading;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// Checks that the settings the old window's bars offered can be reached: Focus Mode's text settings (spacing, width, colour, typeface and what is
/// marked while reading aloud) and the filter and sort of the notes list.
/// </summary>
[NotInParallel]
public sealed class ViewerSettingsWindowsTests
{
    /// <summary>The text that names the button of the settings.</summary>
    private const string ButtonName = "Text settings";

    /// <summary>The colour under the word being read aloud.</summary>
    private const uint WordMark = 0x99D9A400;

    /// <summary>The length of the word marked in the test.</summary>
    private const int WordLength = 3;

    /// <summary>The text that names the text format bar.</summary>
    private const string FormatBar = "Text format";

    /// <summary>A column width in characters, narrower than the default.</summary>
    private const int NarrowWidth = 44;

    /// <summary>The colour of the page for the calm night choice.</summary>
    private const uint NightGround = 0xFF1C2431;

    /// <summary>The colour of the text for the calm night choice.</summary>
    private const uint NightText = 0xFFD6DCE6;

    /// <summary>The window width in pixels.</summary>
    private const int Width = 1000;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 700;

    /// <summary>The pages of the test document.</summary>
    private const int Pages = 1;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 10;

    /// <summary>How long one turn of the session waits for work, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>How many turns a change is given to reach the screen.</summary>
    private const int Turns = 30;

    /// <summary>Focus Mode's bar has a button for the text settings, and pressing it opens a window with every setting the old toolbar had.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FocusModeOffersItsTextSettings()
    {
        var outcome = await WithTab(static (session, tab) =>
        {
            ((ICommand)tab.FocusMode.ToggleCommand).Execute(null);
            Settle(session);
            var bar = session.Chrome.Bars.Select(static item => item.Text).ToHashSet();
            ((ICommand)tab.FocusMode.ShowSettingsCommand).Execute(null);
            Settle(session);
            return new Outcome(bar, session.Chrome.OverlayItems.Select(static item => item.Text).ToHashSet());
        });

        await Assert.That(outcome.Bar.Contains(ButtonName)).IsTrue();
        await Assert.That(outcome.Window.Contains("Text size in points")).IsTrue();
        await Assert.That(outcome.Window.Contains("Space between lines, in text sizes")).IsTrue();
        await Assert.That(outcome.Window.Contains("Space between paragraphs, in text sizes")).IsTrue();
        await Assert.That(outcome.Window.Contains("How wide the column of text is, in characters")).IsTrue();
        await Assert.That(outcome.Window.Any(static text => text.StartsWith("Colour behind the text", StringComparison.Ordinal))).IsTrue();
        await Assert.That(outcome.Window.Any(static text => text.StartsWith("Typeface", StringComparison.Ordinal))).IsTrue();
        await Assert.That(outcome.Window.Any(static text => text.Contains("Mark each word", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>
    /// Focus Mode draws what its settings say: the page colour and the text colour chosen, the serif typeface, and a column narrower or wider with the
    /// width in characters. A setting that only reached the settings window would change nothing on the screen.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FocusModeDrawsItsSettings()
    {
        var plain = await WithTab(ShowFocus);
        var custom = await WithTab(
            static settings =>
            {
                settings.FocusPageColour = FocusPageColour.CalmNight;
                settings.FocusFont = FocusFont.Serif;
                settings.FocusTextWidth = NarrowWidth;
            },
            ShowFocus);

        await Assert.That(plain.Ground).IsEqualTo(0U);
        await Assert.That(plain.Serif).IsFalse();
        await Assert.That(custom.Ground).IsEqualTo(NightGround);
        await Assert.That(custom.Tint).IsEqualTo(NightText);
        await Assert.That(custom.Serif).IsTrue();
        await Assert.That(custom.Width).IsLessThan(plain.Width);
    }

    /// <summary>
    /// Focus Mode shows what is read aloud: the sentence being read is marked, the word being read has a mark under it, and with the band on a
    /// paragraph away from the sentence is drawn faint.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FocusModeMarksWhatIsReadAloud()
    {
        var outcome = await WithTab(
            static settings => settings.ReadAloudHighlight = ReadAloudHighlight.SentenceAndWord,
            static (session, tab) =>
        {
            _ = ShowFocus(session, tab);
            var block = tab.FocusMode.Pages[0].Blocks[0];
            var inside = new TextRange(block.FirstCharacter, block.Text.Length);
            tab.FocusMode.Pages[0].Mark(inside, new(block.FirstCharacter, WordLength), true);
            ((ICommand)tab.FocusMode.LargerCommand).Execute(null);
            Settle(session);
            var reading = session.Chrome.OverlayItems.ToArray();

            tab.FocusMode.Pages[0].Mark(new(block.FirstCharacter + block.Text.Length + WordLength, WordLength), TextRange.None, true);
            ((ICommand)tab.FocusMode.SmallerCommand).Execute(null);
            Settle(session);
            return new Marks(reading, session.Chrome.OverlayItems.ToArray());
        });

        await Assert.That(Array.Exists(outcome.Reading, static item => item is { Kind: ChromeItemKind.Row, Selected: true })).IsTrue();
        await Assert.That(Array.Exists(outcome.Reading, static item => item is { Kind: ChromeItemKind.Panel, Ground: WordMark })).IsTrue();
        await Assert.That(Array.Exists(outcome.Reading, static item => item is { Kind: ChromeItemKind.Row, Dimmed: true })).IsFalse();
        await Assert.That(Array.Exists(outcome.Away, static item => item is { Kind: ChromeItemKind.Row, Dimmed: true })).IsTrue();
        await Assert.That(Array.Exists(outcome.Away, static item => item is { Kind: ChromeItemKind.Panel, Ground: WordMark })).IsFalse();
    }

    /// <summary>Marking the page shows the text format bar the old window had, with its weight, slant, alignment and the way to finish typing, and leaving marking takes it away.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkingShowsTheTextFormatBar()
    {
        var outcome = await WithTab(static (session, tab) =>
        {
            var before = session.Chrome.Bars.Select(static item => item.Text).ToHashSet();
            ((ICommand)tab.Annotations.StartCommand).Execute(null);
            Settle(session);
            var during = session.Chrome.Bars.Select(static item => item.Text).ToHashSet();
            ((ICommand)tab.Annotations.DoneCommand).Execute(null);
            Settle(session);
            return new Outcome(before, during) with { After = session.Chrome.Bars.Select(static item => item.Text).ToHashSet() };
        });

        await Assert.That(outcome.Bar.Contains(FormatBar)).IsFalse();
        await Assert.That(outcome.Window.Contains(FormatBar)).IsTrue();
        await Assert.That(outcome.Window.Contains("Bold")).IsTrue();
        await Assert.That(outcome.Window.Contains("Finish text")).IsTrue();
        await Assert.That(outcome.After!.Contains(FormatBar)).IsFalse();
    }

    /// <summary>The notes list can be filtered and sorted: the window has the old controls.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesListCanBeFilteredAndSorted()
    {
        var outcome = await WithTab(static (session, tab) =>
        {
            ((ICommand)tab.Annotations.ShowFilterCommand).Execute(null);
            Settle(session);
            return new Outcome([], session.Chrome.OverlayItems.Select(static item => item.Text).ToHashSet());
        });

        await Assert.That(outcome.Window.Contains("Words the notes contain")).IsTrue();
        await Assert.That(outcome.Window.Any(static text => text.StartsWith("Sort by", StringComparison.Ordinal))).IsTrue();
        await Assert.That(outcome.Window.Any(static text => text.StartsWith("Author", StringComparison.Ordinal))).IsTrue();
        await Assert.That(outcome.Window.Contains("Show all the notes")).IsTrue();
    }

    /// <summary>The print window has the old sheet settings: how many pages go on a sheet, and the other settings of the old window that apply to a print to a file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintWindowOffersItsSheetSettings()
    {
        var outcome = await WithTab(static (session, tab) =>
        {
            ((ICommand)tab.Printing.PrintCommand).Execute(null);
            Settle(session);
            return new Outcome([], session.Chrome.OverlayItems.Select(static item => item.Text).ToHashSet());
        });

        await Assert.That(outcome.Window.Any(static text => text.StartsWith("Pages per sheet", StringComparison.Ordinal))).IsTrue();
        await Assert.That(outcome.Window.Any(static text => text.StartsWith("Layout", StringComparison.Ordinal))).IsTrue();
        await Assert.That(outcome.Window.Any(static text => text.StartsWith("Copies", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Turns focus mode on and reads how its column is drawn.</summary>
    /// <param name="session">The session.</param>
    /// <param name="tab">The tab.</param>
    /// <returns>The colour of the page, the colour and typeface of the first line of text, and the width of the column.</returns>
    private static FocusLook ShowFocus(ViewerSession session, DocumentTabViewModel tab)
    {
        ((ICommand)tab.FocusMode.ToggleCommand).Execute(null);
        Settle(session);
        var items = session.Chrome.OverlayItems;
        var page = items.First(static item => item.Kind == ChromeItemKind.Popup);
        var line = items.First(static item => item.Kind == ChromeItemKind.Row && item.Text.Length > 0);
        return new(page.Ground, line.Tint, line.Serif, line.Bounds.Width);
    }

    /// <summary>Lets the session handle what is waiting and draws a few frames, so a change has reached the controls.</summary>
    /// <param name="session">The session.</param>
    private static void Settle(ViewerSession session)
    {
        for (var turn = 0; turn < Turns; turn++)
        {
            _ = session.PumpOnce(PumpMilliseconds);
            session.Canvas.DrawFrame();
        }
    }

    /// <summary>Opens a document in a headless window and runs something with the session and its tab.</summary>
    /// <typeparam name="T">What the action gives back.</typeparam>
    /// <param name="act">The action.</param>
    /// <returns>What the action gave.</returns>
    private static Task<T> WithTab<T>(Func<ViewerSession, DocumentTabViewModel, T> act) => WithTab(static _ => { }, act);

    /// <summary>Opens a document in a headless window, started with changed settings, and runs something with the session and its tab.</summary>
    /// <typeparam name="T">What the action gives back.</typeparam>
    /// <param name="configure">Changes the settings before the window starts.</param>
    /// <param name="act">The action.</param>
    /// <returns>What the action gave.</returns>
    private static async Task<T> WithTab<T>(Action<AppSettings> configure, Func<ViewerSession, DocumentTabViewModel, T> act)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-focus-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "document.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateTextFilled(Pages, Lines));
        var before = SynchronizationContext.Current;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            configure(services.Settings);
            using var session = new ViewerSession(window, services, context);
            session.Open([path]);
            for (var turn = 0; turn < Turns * Turns && session.Main.SelectedTab is not { IsLoaded: true }; turn++)
            {
                _ = session.PumpOnce(PumpMilliseconds);
            }

            Settle(session);
            return act(session, session.Main.SelectedTab!);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }
    }

    /// <summary>The controls of focus mode while a sentence is read, and while the sentence read is elsewhere.</summary>
    /// <param name="Reading">The controls while a sentence of the paragraph is read.</param>
    /// <param name="Away">The controls while the sentence read is not in the paragraph.</param>
    private readonly record struct Marks(ChromeItem[] Reading, ChromeItem[] Away);

    /// <summary>How focus mode's column is drawn.</summary>
    /// <param name="Ground">The colour of the page.</param>
    /// <param name="Tint">The colour of the text.</param>
    /// <param name="Serif">Whether the text is set in the serif typeface.</param>
    /// <param name="Width">The width of the column.</param>
    private readonly record struct FocusLook(uint Ground, uint Tint, bool Serif, double Width);

    /// <summary>The texts of the bar and of the settings window.</summary>
    /// <param name="Bar">The texts of the bars.</param>
    /// <param name="Window">The texts of the window.</param>
    private readonly record struct Outcome(HashSet<string> Bar, HashSet<string> Window)
    {
        /// <summary>Gets the texts after the change was undone, when that was looked at.</summary>
        public HashSet<string>? After { get; init; }
    }
}
