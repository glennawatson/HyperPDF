// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.Viewer;
using ReactiveUI.Primitives;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// Checks that the format controls rewrite the picked text box, in a real document. The controls are followed only while a text
/// box is picked, so the check is that a change made after the pick reaches the box.
/// </summary>
[NotInParallel]
public sealed class TextBoxFormatFollowTests
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

    /// <summary>Where the text box is put on the page.</summary>
    private static readonly PagePoint TextAt = new(300, 500);

    /// <summary>A format change made while a text box is picked is written to the box, and one made with nothing picked is not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormatChangeReachesThePickedTextBox()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-format-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        bool made;
        bool behindBeforeChange;
        bool behindAfterChange;
        bool behindWhenNothingPicked;
        try
        {
            var path = Path.Combine(folder, "format.pdf");
            await File.WriteAllBytesAsync(path, TestPdf.CreateTextFilled(Pages, Lines));
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            session.Open([path]);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
            var tab = session.Main.SelectedTab!;
            var annotations = tab.Annotations;
            tab.SidebarMode = SidebarMode.Annotations;
            using var prompt = annotations.PromptInteraction.RegisterHandler(static asked => asked.SetOutput("Styled"));
            using var adding = annotations.AddText(0, TextAt).SubscribeSafe(static _ => { }, static _ => { });
            made = PumpUntil(session, () => annotations.Items.Any(static item => item.Annotation.Kind == AnnotationKind.TextBox));

            annotations.Select(annotations.Items.First(static item => item.Annotation.Kind == AnnotationKind.TextBox).Annotation);
            behindBeforeChange = annotations.ReformatSelected();

            // The change is followed, so the box already has the new format and there is nothing left to write.
            annotations.IsBold = !annotations.IsBold;
            behindAfterChange = annotations.ReformatSelected();

            // With nothing picked the controls change alone; the box keeps its format until it is picked and changed again.
            annotations.Select(null);
            annotations.IsItalic = !annotations.IsItalic;
            behindWhenNothingPicked = annotations.ReformatSelected();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(made).IsTrue();
        await Assert.That(behindBeforeChange).IsFalse();
        await Assert.That(behindAfterChange).IsFalse();
        await Assert.That(behindWhenNothingPicked).IsFalse();
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
