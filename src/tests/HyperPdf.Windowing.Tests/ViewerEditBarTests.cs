// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks the bar that shows while the page's own content is being edited, as the old window's edit bar did.</summary>
[NotInParallel]
public sealed class ViewerEditBarTests
{
    /// <summary>The text that names the bar.</summary>
    private const string BarName = "Edit tools";

    /// <summary>The window width in pixels.</summary>
    private const int Width = 1000;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 700;

    /// <summary>The pages of the test document.</summary>
    private const int Pages = 2;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 10;

    /// <summary>How long one turn of the session waits for work, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>How many turns a change is given to reach the screen.</summary>
    private const int Turns = 30;

    /// <summary>Turning edit mode on puts the edit bar on the screen at once, with the buttons the old window had, and turning it off takes the bar away.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditModeShowsItsBarAndLeavingTakesItAway()
    {
        var outcome = await WithTab(static (session, tab) =>
        {
            var before = BarTexts(session);
            ((ICommand)tab.ContentEdit.ToggleEditingCommand).Execute(null);
            Settle(session);
            var during = BarTexts(session);
            ((ICommand)tab.ContentEdit.ToggleEditingCommand).Execute(null);
            Settle(session);
            return new BarOutcome(before, during, BarTexts(session));
        });

        await Assert.That(outcome.Before.Contains(BarName)).IsFalse();
        await Assert.That(outcome.During.Contains(BarName)).IsTrue();
        await Assert.That(outcome.During.Contains("Remove picked")).IsTrue();
        await Assert.That(outcome.During.Contains("Save PDF 2.0 copy")).IsTrue();
        await Assert.That(outcome.During.Contains("Done")).IsTrue();
        await Assert.That(outcome.After.Contains(BarName)).IsFalse();
    }

    /// <summary>Gets the text of every control of the bars.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The texts.</returns>
    private static HashSet<string> BarTexts(ViewerSession session) => [.. session.Chrome.Bars.Select(static item => item.Text)];

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
    private static async Task<T> WithTab<T>(Func<ViewerSession, HyperPdf.ViewModels.Viewing.DocumentTabViewModel, T> act)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-editbar-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "document.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateTextFilled(Pages, Lines));
        var before = SynchronizationContext.Current;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
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

    /// <summary>The texts of the controls before edit mode, during it and after it.</summary>
    /// <param name="Before">The texts before.</param>
    /// <param name="During">The texts during.</param>
    /// <param name="After">The texts after.</param>
    private readonly record struct BarOutcome(HashSet<string> Before, HashSet<string> During, HashSet<string> After);
}
