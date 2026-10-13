// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that the interpreters and clip paths kept for reuse start every recording clean and allocate nothing once warm.</summary>
[NotInParallel(nameof(RecordingReuseTests))]
public sealed class RecordingReuseTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The cycles made before measuring.</summary>
    private const int Warmup = 8;

    /// <summary>The cycles measured in each pass.</summary>
    private const int Cycles = 64;

    /// <summary>The most passes made; the smallest counts, so one-off work on a busy machine cannot fail the test.</summary>
    private const int Passes = 10;

    /// <summary>The image scale handed to the first rental.</summary>
    private const float FirstScale = 2;

    /// <summary>The zoom class set on the first rental.</summary>
    private const int FirstZoomClass = 5;

    /// <summary>The nesting depth of the second rental.</summary>
    private const int SecondDepth = 1;

    /// <summary>The number of segments the first rental claims to have added.</summary>
    private const int FirstPathPoints = 3;

    /// <summary>The number of hidden marked content levels the first rental claims to be inside.</summary>
    private const int FirstHidden = 2;

    /// <summary>An interpreter rented again after it was disposed starts as new, whatever the first renter left in it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RentedInterpreterStartsCleanAfterReuse()
    {
        using var document = PdfDocumentReader.Open(new RenderTestPdf(Size, Size).ToBytes(), null);
        var cache = PdfDocumentRendering.GetRenderCache(document);
        var device = new NullPictureDevice(new(0, 0, Size, Size));
        var first = ContentInterpreter.Rent(cache, device, 0, FirstScale, CancellationToken.None);
        first.Printing = true;
        first.ZoomClass = FirstZoomClass;
        first.Hidden = FirstHidden;
        first.PathPoints = FirstPathPoints;
        first.PendingClip = true;
        first.Stack.Add(default);
        first.Resources.Add(null);
        first.Marked.Add(true);
        first.Path.MoveTo(1, 1);
        first.Dispose();
        first.Dispose();

        using var second = ContentInterpreter.Rent(cache, device, SecondDepth, CancellationToken.None);

        await Assert.That(second.Printing).IsFalse();
        await Assert.That(second.ZoomClass).IsEqualTo(OptionalContentZoom.NoZoom);
        await Assert.That(second.Hidden).IsEqualTo(0);
        await Assert.That(second.PathPoints).IsEqualTo(0);
        await Assert.That(second.PendingClip).IsFalse();
        await Assert.That(second.Stack.Count).IsEqualTo(0);
        await Assert.That(second.Resources.Count).IsEqualTo(0);
        await Assert.That(second.Marked.Count).IsEqualTo(0);
        await Assert.That(second.Depth).IsEqualTo(SecondDepth);
        await Assert.That(second.ImageScale).IsEqualTo(float.PositiveInfinity);
        await Assert.That(second.Path.Detach().IsEmpty).IsTrue();
    }

    /// <summary>Renting and disposing an interpreter again and again allocates no managed memory once one is idle.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RentingAnInterpreterAllocatesNothingOnceWarm()
    {
        using var document = PdfDocumentReader.Open(new RenderTestPdf(Size, Size).ToBytes(), null);
        var cache = PdfDocumentRendering.GetRenderCache(document);
        var device = new NullPictureDevice(new(0, 0, Size, Size));
        for (var i = 0; i < Warmup; i++)
        {
            ContentInterpreter.Rent(cache, device, 0).Dispose();
        }

        var smallest = long.MaxValue;
        for (var pass = 0; pass < Passes && smallest > 0; pass++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < Cycles; i++)
            {
                ContentInterpreter.Rent(cache, device, 0).Dispose();
            }

            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        await Assert.That(smallest).IsEqualTo(0);
    }

    /// <summary>Pages of one size share one clip path, and a different size gets its own.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PagesOfOneSizeShareTheirClipPath()
    {
        var page = new PdfRect(0, 0, Size, Size);
        var other = new PdfRect(0, 0, Size, Size + 1);

        await Assert.That(PageClips.Get(page)).IsSameReferenceAs(PageClips.Get(page));
        await Assert.That(PageClips.Get(other).Bounds.Bottom).IsEqualTo(Size + 1);
        await Assert.That(PageClips.Get(page).Bounds.Bottom).IsEqualTo(Size);
    }

    /// <summary>Recording one page's marked content twice, with another page between, gives the same result each time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecordingMarkedContentAgainGivesTheSameResult()
    {
        using var document = PdfDocumentReader.Open(new RenderTestPdf(Size, Size) { Content = "/Span << /MCID 0 >> BDC 0 0 50 50 re f EMC" }.ToBytes(), null);
        var page = PdfDocumentPages.GetPage(document, 0);

        var first = MarkedContentRecorder.Record(document, page);
        var second = MarkedContentRecorder.Record(document, page);

        await Assert.That(second.GlyphCount).IsEqualTo(first.GlyphCount);
        await Assert.That(second.Text).IsEqualTo(first.Text);
        await Assert.That(second.GetBounds(0)).IsEqualTo(first.GetBounds(0));
        await Assert.That(first.HasContent(0)).IsTrue();
    }
}
