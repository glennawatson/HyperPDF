// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Document;

/// <summary>Renders pages and manages renderer caches.</summary>
public static class PdfDocumentRendering
{
    /// <summary>Gets the document-owned cache used by content operations outside a page renderer.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The document's shared content cache.</returns>
    internal static PdfRenderCache GetRenderCache(PdfDocument document)
    {
        if (Volatile.Read(ref document.State.RenderCache) is { } existing)
        {
            return existing;
        }

        _ = Interlocked.CompareExchange(ref document.State.RenderCache, new(document), null);
        return Volatile.Read(ref document.State.RenderCache)!;
    }

    /// <summary>Gets the normal render cache shared by renderers with equal image and drawing settings.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The renderer's cache limits and drawing switches.</param>
    /// <returns>The matching cache entry, marked in use by this renderer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfRenderCacheEntry GetRendererCache(PdfDocument document, PdfRenderOptions options) => GetRendererCacheCore(document, options, null, false);

    /// <summary>Gets the output-intent cache shared by renderers with equal image and drawing settings.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The renderer's cache limits and drawing switches.</param>
    /// <param name="outputIntent">The document's cache holding its output-intent colours.</param>
    /// <returns>The matching output-intent cache entry, marked in use by this renderer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfRenderCacheEntry GetOutputIntentRendererCache(PdfDocument document, PdfRenderOptions options, PdfRenderCache outputIntent) =>
        GetRendererCacheCore(document, options, outputIntent.DeviceColors, true);

    /// <summary>Releases one renderer's cache use and closes the cache after its last user leaves.</summary>
    /// <param name="document">The document that owns the cache entry.</param>
    /// <param name="entry">The entry the renderer acquired.</param>
    internal static void ReleaseRendererCache(PdfDocument document, PdfRenderCacheEntry entry)
    {
        lock (document.State.RenderersGate)
        {
            var caches = entry.UsesOutputIntent ? document.State.IntentRendererCaches : document.State.RendererCaches;
            if (caches is null || !caches.TryGetValue(entry.Key, out var current) || !ReferenceEquals(current, entry))
            {
                return;
            }

            entry.Users--;
            if (entry.Users != 0)
            {
                return;
            }

            _ = caches.Remove(entry.Key);
        }

        entry.Cache.Close();
    }

    /// <summary>Records a renderer, so disposing the document releases the renderer's page pictures.</summary>
    /// <param name="document">The document.</param>
    /// <param name="renderer">The renderer.</param>
    internal static void RegisterRenderer(PdfDocument document, PdfPageRenderer renderer)
    {
        lock (document.State.RenderersGate)
        {
            ObjectDisposedException.ThrowIf(document.IsDisposed, document);
            _ = document.State.Renderers.RemoveAll(static reference => !reference.TryGetTarget(out _));
            document.State.Renderers.Add(new(renderer));
        }
    }

    /// <summary>Drops font-dependent results made before newly requested resources were available.</summary>
    /// <param name="document">The document.</param>
    internal static void RefreshFontData(PdfDocument document)
    {
        var generation = document.DrawingServices.FontResourceGeneration;
        if (Volatile.Read(ref document.State.FontDataGeneration) == generation)
        {
            return;
        }

        lock (document.State.RenderersGate)
        {
            if (document.State.FontDataGeneration == generation)
            {
                return;
            }

            Volatile.Read(ref document.State.RenderCache)?.InvalidateFonts();
            Volatile.Read(ref document.State.IntentRenderCache)?.InvalidateFonts();
            if (document.State.RendererCaches is { } normalCaches)
            {
                foreach (var entry in normalCaches.Values)
                {
                    entry.Cache.InvalidateFonts();
                }
            }

            if (document.State.IntentRendererCaches is { } intentCaches)
            {
                foreach (var entry in intentCaches.Values)
                {
                    entry.Cache.InvalidateFonts();
                }
            }

            PdfDocumentPageContent.InvalidatePageContent(document);
            Volatile.Write(ref document.State.FontDataGeneration, generation);
        }
    }

    /// <summary>Releases the page pictures of every renderer and empties the render caches and the text pages.</summary>
    /// <param name="document">The document.</param>
    internal static void ReleaseRendering(PdfDocument document)
    {
        var (renderers, normalCaches, intentCaches) = DetachRendering(document);
        foreach (var reference in renderers)
        {
            if (reference.TryGetTarget(out var renderer))
            {
                renderer.Dispose();
            }
        }

        Volatile.Read(ref document.State.RenderCache)?.Close();
        Volatile.Read(ref document.State.IntentRenderCache)?.Close();
        foreach (var entry in normalCaches)
        {
            entry.Cache.Close();
        }

        foreach (var entry in intentCaches)
        {
            entry.Cache.Close();
        }

        Volatile.Read(ref document.State.TextPages)?.Clear();
    }

    /// <summary>Takes the tracked renderers and render caches away from the document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>What was tracked; empty for a document nothing has rendered from, which has no lock yet.</returns>
    private static DetachedRendering DetachRendering(PdfDocument document)
    {
        // The disposed flag is set before this runs, so no renderer registers while the lock does not exist.
        if (document.State.RenderersGateIfCreated is not { } gate)
        {
            return new([], [], []);
        }

        lock (gate)
        {
            var renderers = document.State.Renderers.ToArray();
            document.State.Renderers.Clear();
            var normalCaches = document.State.RendererCaches is { } normal ? [.. normal.Values] : Array.Empty<PdfRenderCacheEntry>();
            var intentCaches = document.State.IntentRendererCaches is { } intent ? [.. intent.Values] : Array.Empty<PdfRenderCacheEntry>();
            document.State.RendererCaches = null;
            document.State.IntentRendererCaches = null;
            return new(renderers, normalCaches, intentCaches);
        }
    }

    /// <summary>Finds or creates a cache whose options never change after publication.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The renderer options whose image and drawing settings form the key.</param>
    /// <param name="colors">The output-intent colours, or null for fixed device colours.</param>
    /// <param name="outputIntent">Whether the cache is for output-intent rendering.</param>
    /// <returns>The matching cache entry, marked in use.</returns>
    private static PdfRenderCacheEntry GetRendererCacheCore(PdfDocument document, PdfRenderOptions options, OutputIntentColors? colors, bool outputIntent)
    {
        lock (document.State.RenderersGate)
        {
            ObjectDisposedException.ThrowIf(document.IsDisposed, document);
            var hidden = PdfRenderCacheKey.Normalize(options.HiddenColorants);
            var key = new PdfRenderCacheKey(options.ImageCacheBytes, options.SimulateOverprint, options.FormHighlight, PdfRenderCacheKey.Join(hidden), options.Quality);
            var caches = outputIntent
                ? document.State.IntentRendererCaches ??= []
                : document.State.RendererCaches ??= [];
            if (caches.TryGetValue(key, out var existing))
            {
                existing.Users++;
                return existing;
            }

            var cache = new PdfRenderCache(document, colors);
            cache.ImageCapacity = key.ImageCacheBytes;
            cache.SimulateOverprint = key.SimulateOverprint;
            cache.FormHighlight = key.FormHighlight;
            cache.HiddenColorants = hidden;
            cache.Quality = key.Quality;
            var created = new PdfRenderCacheEntry(key, cache, outputIntent);
            caches.Add(key, created);
            return created;
        }
    }

    /// <summary>The renderers and render caches a document tracked when its rendering was released.</summary>
    /// <param name="Renderers">The weakly tracked page renderers.</param>
    /// <param name="NormalCaches">The caches of the normal renderers.</param>
    /// <param name="IntentCaches">The caches of the output-intent renderers.</param>
    private readonly record struct DetachedRendering(WeakReference<PdfPageRenderer>[] Renderers, PdfRenderCacheEntry[] NormalCaches, PdfRenderCacheEntry[] IntentCaches);
}
