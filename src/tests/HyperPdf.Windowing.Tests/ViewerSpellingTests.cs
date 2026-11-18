// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Presentation.Input;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.FormFilling;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>A word typed into a form field that is spelled wrong is offered corrections under the box, and pressing one replaces the word.</summary>
[NotInParallel]
public sealed class ViewerSpellingTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 1100;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 700;

    /// <summary>The height of the sample form's page in points.</summary>
    private const int PageHeight = 792;

    /// <summary>The step, in points, of the search down the page for the text field.</summary>
    private const int FieldStep = 8;

    /// <summary>A point across the page that is inside the sample form's text field.</summary>
    private const float FieldX = 100F;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for one step, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The divisor that finds the middle of a control.</summary>
    private const double Half = 2;

    /// <summary>The word typed, which is the usual slip for the word below.</summary>
    private const string Slip = "teh";

    /// <summary>The word the checker offers for the slip.</summary>
    private const string Fixed = "the";

    /// <summary>The name of the test document.</summary>
    private const string FormFile = "spelling.pdf";

    /// <summary>The corrections show under the box, the first is the right word, and pressing it replaces what was typed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PressingACorrectionReplacesTheWord()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-spelling-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, FormFile), TestPdf.CreateForm());
        var before = SynchronizationContext.Current;
        bool offered;
        bool underlined;
        string first;
        bool replaced;
        string detail;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window))
            { CreateSpellChecker = static () => FakeSpellChecker.Instance };
            using var session = new ViewerSession(window, services, context);
            var chrome = session.Chrome;
            session.Open([Path.Combine(folder, FormFile)]);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true, Forms.HasForm: true });
            PageSettling.Wait(session);
            var tab = session.Main.SelectedTab!;
            _ = tab.Forms.Activate(FindTextField(tab.Forms)!);
            _ = PumpUntil(session, () => chrome.OverlayItems.Any(static item => item.Kind == ChromeItemKind.Field));
            foreach (var character in Slip)
            {
                window.Send(new(WindowEventKind.TextInput, 0, 0, character, 0));
            }

            offered = PumpUntil(session, () => Corrections(chrome).Any());
            underlined = chrome.OverlayItems.Any(static item => item.Kind == ChromeItemKind.Underline);
            detail = $"editing {tab.Forms.Editing?.Name}, focused {tab.Forms.Focused?.Name}, loaded {tab.IsLoaded}, overlay {chrome.HasOverlay}, "
                + $"checks {tab.Forms.ChecksSpelling}, text \"{tab.Forms.EditText}\", items {string.Join(",", chrome.OverlayItems.Select(static item => item.Kind))}";
            var row = Corrections(chrome).FirstOrDefault();
            first = row?.Text ?? string.Empty;
            Click(window, row);
            replaced = PumpUntil(session, () => tab.Forms.EditText == Fixed);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(offered).IsTrue().Because(detail);
        await Assert.That(underlined).IsTrue();
        await Assert.That(first).IsEqualTo(Fixed);
        await Assert.That(replaced).IsTrue();
    }

    /// <summary>Finds the rows of corrections under the typing box.</summary>
    /// <param name="chrome">The window's controls.</param>
    /// <returns>The rows.</returns>
    private static IEnumerable<ChromeItem> Corrections(ViewerChrome chrome) =>
        chrome.OverlayItems.Where(static item => item.Kind == ChromeItemKind.Row && item.Action == OverlayAction.PickRow);

    /// <summary>Finds the text field of the sample form by looking down the page.</summary>
    /// <param name="forms">The form.</param>
    /// <returns>The field, or <see langword="null"/>.</returns>
    private static FormField? FindTextField(FormsViewModel forms)
    {
        for (var y = 0; y < PageHeight; y += FieldStep)
        {
            if (forms.HitTest(0, new(FieldX, y)) is { Kind: FormFieldKind.Text } field)
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>Presses a control with the pointer, in the middle of it.</summary>
    /// <param name="window">The window.</param>
    /// <param name="item">The control, or <see langword="null"/> to press nothing.</param>
    private static void Click(HeadlessWindow window, ChromeItem? item)
    {
        if (item is null)
        {
            return;
        }

        var x = (int)(item.Bounds.X + (item.Bounds.Width / Half));
        var y = (int)(item.Bounds.Y + (item.Bounds.Height / Half));
        window.Send(new(WindowEventKind.PointerMoved, x, y, 0, 0));
        window.Send(new(WindowEventKind.PointerDown, x, y, (uint)ViewerButtons.Left, 0));
        window.Send(new(WindowEventKind.PointerUp, x, y, (uint)ViewerButtons.Left, 0));
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
}
