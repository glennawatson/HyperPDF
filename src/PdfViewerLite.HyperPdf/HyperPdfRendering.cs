// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.Core.Documents;
namespace PdfViewerLite.HyperPdf;

/// <summary>Implements Rendering over the document's owned state.</summary>
internal static class HyperPdfRendering
{
    /// <summary>Gets Renderer.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The current value.</returns>
    internal static PdfPageRenderer GetRenderer(HyperPdfDocument self)
    {
        if (Volatile.Read(ref self.RendererState) is { } existing)
        {
            return existing;
        }

        var created = new PdfPageRenderer(self.Document, HyperPdfFormRuntime.CreateRenderOptions(self));
        if (Interlocked.CompareExchange(ref self.RendererState, created, null) is { } winner)
        {
            created.Dispose();
            return winner;
        }

        return created;
    }

    /// <summary>Loads external page resources before rendering or reading text.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels resource I/O.</param>
    /// <returns>A task completing when page resources are ready.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask PreparePageAsync(HyperPdfDocument self, int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return PageToPrepare(self, pageIndex) is { } page
            ? PdfDocumentPages.PrefetchPageAsync(self.Document, page, cancellationToken)
            : ValueTask.CompletedTask;
    }

    /// <summary>Renders part of a page into a pixel buffer. The buffer is filled with white first.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="info">What to render.</param>
    /// <param name="target">The destination buffer.</param>
    /// <returns><see langword="true"/> when the page rendered; <see langword="false"/> when the document is closed.</returns>
    internal static bool Render(HyperPdfDocument self, in PageRenderInfo info, RenderTarget target)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed || (uint)info.PageIndex >= (uint)self.PageCount)
        {
            return false;
        }

        var request = new PdfTileRequest(info.PageIndex, info.Scale, (int)info.Rotation, info.OffsetX, info.OffsetY, ToLibraryFlags(info.Flags));
        var pixels = new PdfTileTarget(target.Pixels, target.Width, target.Height, target.Stride);
        try
        {
            return GetRenderer(self).Render(request, pixels);
        }
        catch (ObjectDisposedException) when (!self.IsDisposed)
        {
            // An edit replaced the renderer while this tile started; draw it once more with the new one.
            return GetRenderer(self).Render(request, pixels);
        }
    }

    /// <summary>Records the requested content after its external resources are ready.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="info">The page and flags.</param>
    /// <param name="cancellationToken">Cancels recording.</param>
    /// <returns>Whether the page was recorded.</returns>
    internal static bool Prepare(HyperPdfDocument self, in PageRenderInfo info, CancellationToken cancellationToken)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed || (uint)info.PageIndex >= (uint)self.PageCount)
        {
            return false;
        }

        var request = ToRequest(info);
        return GetRenderer(self).Prepare(request, cancellationToken);
    }

    /// <summary>Loads external page resources, then records the requested content.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="info">The page and flags.</param>
    /// <param name="cancellationToken">Cancels loading and recording.</param>
    /// <returns>Whether the page was recorded.</returns>
    internal static async ValueTask<bool> PrepareAsync(HyperPdfDocument self, PageRenderInfo info, CancellationToken cancellationToken)
    {
        await PreparePageAsync(self, info.PageIndex, cancellationToken).ConfigureAwait(false);
        return Prepare(self, info, cancellationToken);
    }

    /// <summary>Replays a page on the graphics thread into a caller-owned target.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="info">The tile region and flags.</param>
    /// <param name="target">The target retained through presentation.</param>
    /// <returns>Whether drawing was submitted.</returns>
    internal static bool RenderGpu(HyperPdfDocument self, in PageRenderInfo info, IPdfRenderTarget target)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed || (uint)info.PageIndex >= (uint)self.PageCount)
        {
            return false;
        }

        var request = ToRequest(info);
        try
        {
            return GetRenderer(self).RenderToTarget(request, target);
        }
        catch (ObjectDisposedException) when (!self.IsDisposed)
        {
            return GetRenderer(self).RenderToTarget(request, target);
        }
    }

    /// <summary>Maps viewer render flags to library render flags.</summary>
    /// <param name="flags">The viewer flags.</param>
    /// <returns>The library flags.</returns>
    internal static PdfRenderFlags ToLibraryFlags(RenderFlags flags)
    {
        var result = PdfRenderFlags.None;
        if ((flags & RenderFlags.Annotations) != 0)
        {
            result |= PdfRenderFlags.Annotations;
        }

        if ((flags & RenderFlags.Grayscale) != 0)
        {
            result |= PdfRenderFlags.Grayscale;
        }

        if ((flags & RenderFlags.Printing) != 0)
        {
            result |= PdfRenderFlags.Printing;
        }

        if ((flags & RenderFlags.FixedDeviceColors) != 0)
        {
            result |= PdfRenderFlags.FixedDeviceColors;
        }

        return result;
    }

    /// <summary>Drops the renderer and its page pictures; tiles drawing with it finish, and the next tile makes a new one.</summary>
    /// <param name="self">The owning document.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ResetRenderer(HyperPdfDocument self) => Interlocked.Exchange(ref self.RendererState, null)?.Dispose();

    /// <summary>Captures a page while its position is protected from edits.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The captured page, or null when it no longer exists.</returns>
    internal static PdfPage? PageToPrepare(HyperPdfDocument self, int pageIndex)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        return self.IsDisposed || (uint)pageIndex >= (uint)self.PageCount ? null : PdfDocumentPages.GetPage(self.Document, pageIndex);
    }

    /// <summary>Maps the viewer's tile request to the engine's rendering contract.</summary>
    /// <param name="info">The viewer request.</param>
    /// <returns>The engine request.</returns>
    private static PdfTileRequest ToRequest(in PageRenderInfo info) =>
        new(info.PageIndex, info.Scale, (int)info.Rotation, info.OffsetX, info.OffsetY, ToLibraryFlags(info.Flags));
}
