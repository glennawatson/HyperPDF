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
using HyperPdf.Viewer.Icons;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>Verifies the non-modal on-page comment bubble anchored beside sticky notes and commented passages.</summary>
[NotInParallel]
public sealed class NoteBubbleOverlaysTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 1100;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 750;

    /// <summary>The lines of text on the sample page.</summary>
    private const int Lines = 5;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for one step, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The page coordinate where the new sticky note is placed.</summary>
    private const float NoteCoordinate = 120F;

    /// <summary>An x coordinate inside the page canvas away from the bubble card.</summary>
    private const int OutsideOffset = 12;

    /// <summary>Minimum left margin needed to click to the left of the bubble.</summary>
    private const int OutsideClearance = 24;

    /// <summary>The text typed into the new sticky note.</summary>
    private const string TypedComment = "Check figure margin";

    /// <summary>Opening a note bubble places a non-modal card over the page, saves typed words on Enter, offers Reply and Delete on reopen, and closes on outside click.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NoteBubble_NewAndExistingNote_SavesOnPageAndClosesOnOutsideClick()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-bubble-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        BubbleOutcome outcome;
        try
        {
            outcome = await RunNoteBubbleAsync(folder);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.OpenedInsideCanvas).IsTrue();
        await Assert.That(outcome.SavedContents).IsEqualTo(TypedComment);
        await Assert.That(outcome.ReopenedWithReplyAndDelete).IsTrue();
        await Assert.That(outcome.ClosedOnOutsideClick).IsTrue();
    }

    /// <summary>Drives creating and reopening a sticky note through the on-page bubble.</summary>
    /// <param name="folder">The temporary test directory.</param>
    /// <returns>The observed bubble behaviour.</returns>
    private static async Task<BubbleOutcome> RunNoteBubbleAsync(string folder)
    {
        var path = Path.Combine(folder, "notes.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateTextFilled(1, Lines));
        using var window = new HeadlessWindow(Width, Height);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        var chrome = session.Chrome;
        session.Open([path]);
        _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
        PageSettling.Wait(session);
        var marks = session.Main.SelectedTab!.Annotations;

        _ = marks.BeginNoteBubble(0, new(NoteCoordinate, NoteCoordinate));
        var opened = PumpUntil(session, () => chrome.HasOverlay && chrome.OverlayItems.Exists(static item => item.Kind == ChromeItemKind.Field));
        var popup = chrome.OverlayItems.First(static item => item.Kind == ChromeItemKind.Popup);
        var canvas = chrome.CanvasBounds;
        var insideCanvas = opened && popup.Bounds.X >= canvas.X && popup.Bounds.Right <= canvas.Right;

        foreach (var ch in TypedComment)
        {
            window.Send(new(WindowEventKind.TextInput, 0, 0, ch, 0));
        }

        _ = PumpUntil(session, () => marks.BubbleNoteText == TypedComment);
        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.Enter, 0));
        _ = PumpUntil(session, () => !chrome.HasOverlay);

        var created = marks.Find(0, 0);
        _ = marks.OpenBubble(created);
        var reopened = PumpUntil(session, () => chrome.HasOverlay && chrome.OverlayItems.Exists(static item => item.Text == "Reply"));
        var hasActions = reopened
            && chrome.OverlayItems.Exists(static item => item.Text == "Reply" && item.Icon == ChromeIcon.Note)
            && chrome.OverlayItems.Exists(static item => item.Text == "Delete" && item.Icon == ChromeIcon.Close);

        var reopenedPopup = chrome.OverlayItems.First(static item => item.Kind == ChromeItemKind.Popup);
        var outsideX = reopenedPopup.Bounds.X > canvas.X + OutsideClearance ? (int)canvas.X + OutsideOffset : (int)canvas.Right - OutsideOffset;
        var outsideY = (int)canvas.Bottom - OutsideOffset;
        window.Send(new(WindowEventKind.PointerDown, outsideX, outsideY, (uint)ViewerButtons.Left, 0));
        window.Send(new(WindowEventKind.PointerUp, outsideX, outsideY, (uint)ViewerButtons.Left, 0));
        var closedOutside = PumpUntil(session, () => !chrome.HasOverlay && !marks.HasBubble);

        return new(insideCanvas, created?.Contents ?? string.Empty, hasActions, closedOutside);
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

    /// <summary>Records the results of driving the on-page note bubble.</summary>
    /// <param name="OpenedInsideCanvas">Whether the bubble opened inside the page canvas.</param>
    /// <param name="SavedContents">The saved note text on the PDF page.</param>
    /// <param name="ReopenedWithReplyAndDelete">Whether reopening an existing note offered Reply and Delete with icons.</param>
    /// <param name="ClosedOnOutsideClick">Whether clicking outside on the page dismissed the bubble.</param>
    private readonly record struct BubbleOutcome(bool OpenedInsideCanvas, string SavedContents, bool ReopenedWithReplyAndDelete, bool ClosedOnOutsideClick);
}
