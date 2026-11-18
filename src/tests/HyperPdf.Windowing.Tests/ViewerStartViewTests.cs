// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Platform;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Viewer.Icons;
using HyperPdf.Windowing.Windows;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks the view of a window with no document: what the old start view offered, a hint, the documents opened lately and a way to clear them.</summary>
[NotInParallel]
public sealed class ViewerStartViewTests
{
    /// <summary>The start of the text of the button that forgets a document opened lately.</summary>
    private const string RemoveName = "Remove";

    /// <summary>The documents opened lately that the test lists.</summary>
    private const int Listed = 2;

    /// <summary>The window width in pixels.</summary>
    private const int Width = 900;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 600;

    /// <summary>How long one turn of the session waits for work, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>How many turns a change is given to reach the screen.</summary>
    private const int Turns = 30;

    /// <summary>The start view tells how to open files, lists the documents opened lately, and its Clear list row empties that list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StartViewListsRecentDocumentsAndClearsThem()
    {
        var recent = new ListRecentDocuments();
        recent.Add("/documents/first.pdf");
        recent.Add("/documents/second.pdf");
        var before = SynchronizationContext.Current;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-start-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(folder);
            try
            {
                using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new RecentPlatform(recent), new ViewerRenderHead(window));
                using var session = new ViewerSession(window, services, context);
                Settle(session);
                var shown = session.Chrome.OverlayItems.Select(static item => item.Text).ToArray();

                // Each document has its icon and the folder it is in, and a rule sets it apart from the next: one under the heading and one under each.
                var rules = session.Chrome.OverlayItems.Count(static item => item.Kind == ChromeItemKind.Separator);
                var documents = session.Chrome.OverlayItems.Count(static item => item is { Kind: ChromeItemKind.Row, Icon: ChromeIcon.Document });
                var folders = shown.Count(static text => text == "/documents");
                var open = session.Chrome.OverlayItems.First(static item => item.Icon == ChromeIcon.Open);
                var opens = ReferenceEquals(open.Command, session.Main.OpenCommand);

                // The newest document is listed first, and its own button forgets just that one.
                Press(window, session.Chrome.OverlayItems.First(static item => item.Text.StartsWith(RemoveName, StringComparison.Ordinal)));
                Settle(session);
                var left = recent.GetRecent(int.MaxValue).Select(static item => item.FilePath).ToArray();

                Press(window, session.Chrome.OverlayItems.First(static item => item.Text == "Clear list"));
                Settle(session);

                await Assert.That(shown.Contains("Drop PDF files here, or choose them from a folder.")).IsTrue();
                await Assert.That(shown.Contains("first.pdf")).IsTrue();
                await Assert.That(shown.Contains("second.pdf")).IsTrue();
                await Assert.That(documents).IsEqualTo(Listed);
                await Assert.That(folders).IsEqualTo(Listed);
                await Assert.That(rules).IsEqualTo(Listed + 1);
                await Assert.That(opens).IsTrue();
                await Assert.That(left.Length).IsEqualTo(1);
                await Assert.That(left[0]).IsEqualTo("/documents/first.pdf");
                await Assert.That(recent.GetRecent(int.MaxValue).Count).IsEqualTo(0);
                await Assert.That(session.Main.RecentDocuments.Count).IsEqualTo(0);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
        }
    }

    /// <summary>Presses and lets go of the left button on a control.</summary>
    /// <param name="window">The window.</param>
    /// <param name="item">The control.</param>
    private static void Press(HeadlessWindow window, ChromeItem item)
    {
        var x = (int)(item.Bounds.X + 1);
        var y = (int)(item.Bounds.Y + 1);
        window.Send(new(WindowEventKind.PointerMoved, x, y, 0, 0));
        window.Send(new(WindowEventKind.PointerDown, x, y, (uint)HyperPdf.Presentation.Input.ViewerButtons.Left, 0));
        window.Send(new(WindowEventKind.PointerUp, x, y, (uint)HyperPdf.Presentation.Input.ViewerButtons.Left, 0));
    }

    /// <summary>Lets the session handle what is waiting and draws a few frames.</summary>
    /// <param name="session">The session.</param>
    private static void Settle(ViewerSession session)
    {
        for (var turn = 0; turn < Turns; turn++)
        {
            _ = session.PumpOnce(PumpMilliseconds);
            session.Canvas.DrawFrame();
        }
    }

    /// <summary>A desktop with a recent documents list that keeps what it is given, and nothing else.</summary>
    /// <param name="recent">The recent documents.</param>
    private sealed class RecentPlatform(IRecentDocumentStore recent) : IDesktopPlatform
    {
        /// <summary>The integration the rest is taken from.</summary>
        private readonly FallbackPlatform _fallback = new();

        /// <inheritdoc/>
        public string Name => "Test";

        /// <inheritdoc/>
        public IDesktopThemeSource? ThemeSource => null;

        /// <inheritdoc/>
        public IFileManagerLauncher FileManager => _fallback.FileManager;

        /// <inheritdoc/>
        public IRecentDocumentStore RecentDocuments => recent;

        /// <inheritdoc/>
        public IPrintService Printer => _fallback.Printer;

        /// <inheritdoc/>
        public string? GetLaunchActivationToken() => null;

        /// <inheritdoc/>
        public HyperPdf.Core.Speech.IAudioOutput CreateAudioOutput() => new HyperPdf.Core.Speech.NullAudioOutput();

        /// <inheritdoc/>
        public IObservable<bool> TryForward(OpenRequest request) => Signal.Return(false);

        /// <inheritdoc/>
        public IObservable<ISingleInstance?> TryClaimSingleInstance() => Signal.Return<ISingleInstance?>(null);
    }

    /// <summary>A recent documents list in memory.</summary>
    private sealed class ListRecentDocuments : IRecentDocumentStore
    {
        /// <summary>The documents, newest first.</summary>
        private readonly List<RecentDocument> _items = [];

        /// <inheritdoc/>
        public IReadOnlyList<RecentDocument> GetRecent(int maxCount) => [.. _items.Take(maxCount)];

        /// <inheritdoc/>
        public void Add(string filePath) => _items.Insert(0, new(filePath, DateTimeOffset.UnixEpoch));

        /// <inheritdoc/>
        public void Remove(string filePath) => _items.RemoveAll(item => item.FilePath == filePath);

        /// <inheritdoc/>
        public void Clear() => _items.Clear();
    }
}
