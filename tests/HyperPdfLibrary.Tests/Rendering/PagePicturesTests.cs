// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks how a page's recorded pictures behave when recording is paused, cancelled, fails or is retired.</summary>
[NotInParallel]
public sealed class PagePicturesTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>Rectangles drawn, enough for several slices of recording.</summary>
    private const int Rectangles = 1500;

    /// <summary>The idle interpreters the failed-start check takes out of the pool before it begins.</summary>
    private const int PoolDrain = 64;

    /// <summary>How long the test waits for a thread that must not be blocked.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>A recording cancelled between slices is discarded and counts as nothing recorded; the next call records it afresh.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ACancelledRecordingCountsAsNothingRecorded()
    {
        using var test = new RenderTestPage(CreateBusyPage());
        var page = PdfDocumentPages.GetPage(test.Document, 0);
        var entry = new PagePictures(0, 0);
        using var source = new CancellationTokenSource();

        entry.Acquire();
        var cancelled = entry.ContinueContent(
            test.Renderer.Cache,
            page,
            false,
            () =>
            {
                // Cancels after the first slice, as a tab switch would.
                source.Cancel();
                return false;
            },
            source.Token);
        var recordedAfterCancel = entry.Recordings;
        var finished = entry.ContinueContent(test.Renderer.Cache, page, false, null, CancellationToken.None);
        var recordedAfterFinish = entry.Recordings;
        entry.Release();
        entry.Retire();

        await Assert.That(cancelled).IsEqualTo(PdfRenderStatus.Cancelled);
        await Assert.That(recordedAfterCancel).IsEqualTo(0);
        await Assert.That(finished).IsEqualTo(PdfRenderStatus.Done);
        await Assert.That(recordedAfterFinish).IsEqualTo(1);
    }

    /// <summary>The pause callback runs with no lock held, so it can use the entry from another thread.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThePauseCallbackRunsWithoutTheEntryLocked()
    {
        using var test = new RenderTestPage(CreateBusyPage());
        var page = PdfDocumentPages.GetPage(test.Document, 0);
        var entry = new PagePictures(0, 0);
        var reachedEntry = false;

        entry.Acquire();
        var status = entry.ContinueContent(
            test.Renderer.Cache,
            page,
            false,
            () =>
            {
                // Another thread asking the entry for its annotations needs the entry lock.
                var other = new Thread(() => _ = entry.GetAnnotations(test.Renderer.Cache, page, false)) { IsBackground = true };
                other.Start();
                reachedEntry = other.Join(Patience);
                return true;
            },
            CancellationToken.None);
        entry.Release();
        entry.Retire();

        await Assert.That(status).IsEqualTo(PdfRenderStatus.Paused);
        await Assert.That(reachedEntry).IsTrue();
    }

    /// <summary>Retiring an entry that is in use leaves its pictures to the last user, and counting never waits for a recording.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RetiringAnEntryInUseKeepsItsPicturesUntilTheLastRelease()
    {
        using var test = new RenderTestPage(CreateBusyPage());
        var page = PdfDocumentPages.GetPage(test.Document, 0);
        var entry = new PagePictures(0, 0);

        entry.Acquire();
        _ = entry.GetContent(test.Renderer.Cache, page, false);
        entry.Retire();
        var heldWhileInUse = entry.HeldBytes;
        entry.Release();

        await Assert.That(heldWhileInUse).IsGreaterThan(0);
        await Assert.That(entry.HeldBytes).IsEqualTo(0);
    }

    /// <summary>A recording whose start fails gives its interpreter back for the next recording to reuse.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFailedStartReturnsItsInterpreter()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var content = pdf.AddStream("/Filter /ASCIIHexDecode", "0 g>");
        pdf.PageEntries = $"/Contents {content} 0 R";
        using var test = new RenderTestPage(pdf.ToBytes());
        var page = PdfDocumentPages.GetPage(test.Document, 0);
        using var device = PageRecorder.Begin(test.Renderer.Cache, page);
        var held = new List<ContentInterpreter>();
        for (var i = 0; i < PoolDrain; i++)
        {
            held.Add(ContentInterpreter.Rent(test.Renderer.Cache, device, 0));
        }

        var idle = ContentInterpreter.Rent(test.Renderer.Cache, device, 0);
        idle.Dispose();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var failure = Capture(() => ProgressiveRecording.Start(test.Renderer.Cache, page, false, float.PositiveInfinity, 0, cancelled.Token));
        var again = ContentInterpreter.Rent(test.Renderer.Cache, device, 0);
        var reused = ReferenceEquals(idle, again);
        again.Dispose();
        foreach (var interpreter in held)
        {
            interpreter.Dispose();
        }

        await Assert.That(failure).IsTypeOf<OperationCanceledException>();
        await Assert.That(reused).IsTrue();
    }

    /// <summary>Builds a page with many small fills, so recording takes several slices.</summary>
    /// <returns>The PDF.</returns>
    private static byte[] CreateBusyPage()
    {
        var content = new StringBuilder();
        for (var i = 0; i < Rectangles; i++)
        {
            _ = content.Append("0 0 1 1 re f ");
        }

        return new RenderTestPdf(Size, Size) { Content = content.ToString() }.ToBytes();
    }

    /// <summary>Runs an action and returns what it threw.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The exception, or <see langword="null"/>.</returns>
    private static Exception? Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
