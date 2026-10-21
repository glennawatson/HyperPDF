// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>
/// Renders the area dragged on a page with the Snapshot tool at twice the screen's resolution or 200 dots per inch, and
/// puts it on the clipboard as a picture (or saves it as a PNG file where the desktop clipboard cannot hold pictures).
/// </summary>
internal sealed class PageSnapshotter
{
    /// <summary>The points per inch, to give the picture its true resolution.</summary>
    private const double PointsPerInch = 72;

    /// <summary>Said when the picture is on the clipboard.</summary>
    private const string CopiedMessage = "Copied an image of the area. Paste it where you need it.";

    /// <summary>Said when the picture could not be made.</summary>
    private const string FailedMessage = "Could not make an image of the area.";

    /// <summary>The last picture put on the clipboard, kept because some desktops read it only when it is pasted.</summary>
    private Bitmap? _snapshot;

    /// <summary>Completes when the latest snapshot asked for has finished, so the next one can take its turn.</summary>
    private Task _lastSnapshot = Task.CompletedTask;

    /// <summary>The snapshots asked for.</summary>
    private int _snapshotsRequested;

    /// <summary>The snapshots that have taken their turn and started.</summary>
    private int _snapshotsStarted;

    /// <summary>The snapshots that have finished, whether they made a picture or not.</summary>
    private int _snapshotsFinished;

    /// <summary>Gets how many snapshots have been asked for.</summary>
    internal int SnapshotsRequested => Volatile.Read(ref _snapshotsRequested);

    /// <summary>Gets how many snapshots have taken their turn and started.</summary>
    internal int SnapshotsStarted => Volatile.Read(ref _snapshotsStarted);

    /// <summary>Gets how many snapshots have finished.</summary>
    internal int SnapshotsFinished => Volatile.Read(ref _snapshotsFinished);

    /// <summary>Releases the last rendered snapshot bitmap when the canvas is deactivated.</summary>
    internal void Release()
    {
        _snapshot?.Dispose();
        _snapshot = null;
    }

    /// <summary>Copies a picture of an area of the page under its middle, saying what happened.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="area">The area, in canvas coordinates.</param>
    /// <returns>A task.</returns>
    internal async Task SnapshotAsync(PageCanvas canvas, DocumentTabViewModel tab, Rect area)
    {
        // One snapshot at a time: a second must not dispose the picture the first is still copying or saving.
        _ = Interlocked.Increment(ref _snapshotsRequested);
        var previous = _lastSnapshot;
        var turn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _lastSnapshot = turn.Task;
        await previous.ConfigureAwait(true);
        _ = Interlocked.Increment(ref _snapshotsStarted);
        try
        {
            if (FindSnapshotRegion(canvas, in area, out var page) is not { IsEmpty: false } region || tab.TryGetDocument() is not { } document)
            {
                return;
            }

            await document.PreparePageAsync(page, CancellationToken.None).ConfigureAwait(true);
            var job = new SnapshotRenderJob(document, page, tab.Rotation, region);
            var bitmap = await Task.Factory.StartNew(RenderSnapshot, job, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
                .ConfigureAwait(true);
            if (bitmap is null)
            {
                tab.Notice = FailedMessage;
                return;
            }

            _snapshot?.Dispose();
            _snapshot = bitmap;
            await CopyOrSaveAsync(canvas, tab, bitmap).ConfigureAwait(true);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            ErrorTrace.Write(error);
            tab.Notice = FailedMessage;
        }
        finally
        {
            _ = Interlocked.Increment(ref _snapshotsFinished);
            _ = turn.TrySetResult();
        }
    }

    /// <summary>Works out what to render for an area: the page under its middle and the part of that page inside it.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="area">The area, in canvas coordinates.</param>
    /// <param name="page">The page.</param>
    /// <returns>The region, or <see langword="null"/> when the area is off the pages.</returns>
    private static SnapshotRegion? FindSnapshotRegion(PageCanvas canvas, in Rect area, out int page)
    {
        var centre = area.Center;
        page = canvas.Layout.HitTest(centre.X, centre.Y);
        if (page < 0)
        {
            return null;
        }

        var bounds = canvas.Layout.GetPageBounds(page);
        var pageRect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var inside = area.Intersect(pageRect);
        var scaling = TopLevel.GetTopLevel(canvas)?.RenderScaling ?? 1;
        return SnapshotRegion.Create(inside.X - pageRect.X, inside.Y - pageRect.Y, inside.Width, inside.Height, canvas.Layout.Options.Scale, scaling);
    }

    /// <summary>Puts a picture on the clipboard, or saves it where the user chooses when the clipboard cannot hold pictures.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="bitmap">The picture.</param>
    /// <returns>A task.</returns>
    private static async Task CopyOrSaveAsync(PageCanvas canvas, DocumentTabViewModel tab, Bitmap bitmap)
    {
        if (TopLevel.GetTopLevel(canvas)?.Clipboard is { } clipboard)
        {
            try
            {
                await clipboard.SetBitmapAsync(bitmap).ConfigureAwait(true);
                tab.Notice = CopiedMessage;
                return;
            }
            catch (NotSupportedException error)
            {
                // Some desktops' clipboards hold only text; the picture is saved instead.
                ErrorTrace.Write(error);
            }
        }

        var path = await tab.PageTools.SaveImageInteraction.Handle(tab.PageTools.SnapshotName).ToTask().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            bitmap.Save(path, new PngBitmapEncoderOptions());
            tab.Notice = $"This desktop's clipboard cannot hold images, so the image of the area was saved as {Path.GetFileName(path)}.";
        }
        catch (IOException error)
        {
            tab.Notice = $"Could not save the image: {error.Message}";
        }
        catch (UnauthorizedAccessException error)
        {
            tab.Notice = $"Could not save the image: {error.Message}";
        }
    }

    /// <summary>Renders part of a page into a new bitmap, off the user interface thread.</summary>
    /// <param name="state">The boxed <see cref="SnapshotRenderJob"/>.</param>
    /// <returns>The picture, or <see langword="null"/> when rendering failed.</returns>
    private static unsafe WriteableBitmap? RenderSnapshot(object? state)
    {
        var (document, page, rotation, region) = (SnapshotRenderJob)state!;
        var dpi = region.Scale * PointsPerInch;
        var bitmap = new WriteableBitmap(new PixelSize(region.Width, region.Height), new Vector(dpi, dpi), PixelFormat.Bgra8888, AlphaFormat.Premul);
        bool rendered;
        using (var buffer = bitmap.Lock())
        {
            var pixels = new Span<byte>((void*)buffer.Address, buffer.RowBytes * buffer.Size.Height);
            var info = new PageRenderInfo(page, region.Scale, rotation, region.OffsetX, region.OffsetY, RenderFlags.Annotations);
            rendered = document.Render(info, new(pixels, region.Width, region.Height, buffer.RowBytes));
        }

        if (rendered)
        {
            return bitmap;
        }

        bitmap.Dispose();
        return null;
    }

    /// <summary>Describes a snapshot render job passed to the background worker.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Page">The page index.</param>
    /// <param name="Rotation">The page rotation.</param>
    /// <param name="Region">The pixel region to render.</param>
    private readonly record struct SnapshotRenderJob(IDocument Document, int Page, PageRotation Rotation, SnapshotRegion Region);
}
