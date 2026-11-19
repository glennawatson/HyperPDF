// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Finding;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Output;
using HyperPdf.ViewModels.Preferences;
using HyperPdf.ViewModels.Reading;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Signing;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;
using ReactiveUI.Primitives;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// Checks that showing a tab does not make the parts of it that are made on first use, and that the window still answers the
/// questions of such a part once it is made.
/// </summary>
[NotInParallel]
public sealed class TabPartsMadeOnUseTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 900;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 600;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for one step, in seconds.</summary>
    private const double TimeoutSeconds = 10;

    /// <summary>The pages of the document.</summary>
    private const int Pages = 2;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 4;

    /// <summary>The title of the question asked.</summary>
    private const string Title = "save as 2.0";

    /// <summary>A tab that is shown has made no copy commands, and their question opens a dialog once they are made.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopyCommandsAreMadeOnUseAndThenAnswered()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-parts-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        var made = new List<object>();
        int madeWhenShown;
        bool loaded;
        bool asked;
        try
        {
            var path = Path.Combine(folder, "parts.pdf");
            await File.WriteAllBytesAsync(path, TestPdf.CreateTextFilled(Pages, Lines));
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            session.Open([path]);
            loaded = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
            var tab = session.Main.SelectedTab!;
            using var watching = tab.MadeParts.SubscribeSafe(made.Add, static _ => { });
            madeWhenShown = made.Count(static part => part
                is TabCopyActionSet or TagsViewModel or OutlineEditViewModel or DocumentPartsViewModel or TextRecognitionViewModel
                or ReadAloudViewModel or FocusModeViewModel or SearchViewModel or PageManagementViewModel or MeasureViewModel
                or AnnotationsViewModel or TabFileLinks or TabPrintingViewModel or SignaturesViewModel or DocumentMediaViewModel or LayersViewModel
                or FillAndSignViewModel or PageContentEditViewModel);

            using var asking = tab.Copies.Pdf20SaveInteraction.Handle(new(Title, "Go ahead?", "Yes")).ToSignal().SubscribeSafe(static _ => { }, static _ => { });
            asked = PumpUntil(session, () => session.Chrome.OverlayItems.Exists(static item => item.Kind == ChromeItemKind.Heading && item.Text == Title));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(loaded).IsTrue();
        await Assert.That(madeWhenShown).IsEqualTo(0);
        await Assert.That(made.OfType<TabCopyActionSet>().Count()).IsEqualTo(1);
        await Assert.That(asked).IsTrue();
    }

    /// <summary>Runs the loop until something is true, or the wait runs out.</summary>
    /// <param name="session">The session.</param>
    /// <param name="done">Tells when to stop.</param>
    /// <returns><see langword="true"/> when it became true in time.</returns>
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
