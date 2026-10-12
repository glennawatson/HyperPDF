// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Finds the content streams veraPDF reads: each page's contents and annotation appearances, and the forms, tiling
/// patterns and Type 3 glyphs those streams use. Each is read once with <see cref="PdfASyntaxContent"/>.
/// </summary>
internal static class PdfASyntaxStreams
{
    /// <summary>The appearance entries veraPDF reads: normal, rollover and down.</summary>
    private static readonly KnownName[] AppearanceKeys = [KnownName.N, KnownName.R, KnownName.D];

    /// <summary>Reads every content stream the document's pages use.</summary>
    /// <param name="scan">The pass.</param>
    internal static void Check(PdfAFileScan scan)
    {
        var walk = new PdfASyntaxWalk(scan);
        var document = scan.Context.Document;
        for (var index = 0; index < document.PageCount; index++)
        {
            scan.CancellationToken.ThrowIfCancellationRequested();
            var page = PdfDocumentPages.GetPage(document, index);
            QueuePage(walk, page);
            Drain(walk);
        }
    }

    /// <summary>Queues a form XObject the content draws.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="name">The XObject's resource name.</param>
    internal static void QueueXObject(PdfASyntaxWalk walk, PdfName name)
    {
        if (StoreReading.Resolve(walk.Store, walk.Resources?.GetDictionary(KnownName.XObject)?.GetRaw(name) ?? default).AsStream() is { } form
            && form.Dictionary.IsName(KnownName.Subtype, KnownName.Form))
        {
            Queue(walk, [form], form.Dictionary.GetDictionary(KnownName.Resources) ?? walk.Resources);
        }
    }

    /// <summary>Queues a tiling pattern the content paints with.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="name">The pattern's resource name.</param>
    internal static void QueuePattern(PdfASyntaxWalk walk, PdfName name)
    {
        if (StoreReading.Resolve(walk.Store, walk.Resources?.GetDictionary(KnownName.Pattern)?.GetRaw(name) ?? default).AsStream() is { } pattern)
        {
            Queue(walk, [pattern], pattern.Dictionary.GetDictionary(KnownName.Resources) ?? walk.Resources);
        }
    }

    /// <summary>Queues the glyph procedures of the Type 3 font the content shows text with.</summary>
    /// <param name="walk">The walk.</param>
    internal static void QueueType3Glyphs(PdfASyntaxWalk walk)
    {
        if (walk.Font is not { } font || !font.IsName(KnownName.Subtype, KnownName.Type3) || font.GetDictionary(KnownName.CharProcs) is not { } procedures)
        {
            return;
        }

        var resources = font.GetDictionary(KnownName.Resources) ?? walk.Resources;
        for (var i = 0; i < procedures.Count; i++)
        {
            if (StoreReading.Resolve(walk.Store, procedures.GetValueAt(i)).AsStream() is { } glyph)
            {
                Queue(walk, [glyph], resources);
            }
        }
    }

    /// <summary>Queues a page's contents and its annotations' appearances.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="page">The page.</param>
    private static void QueuePage(PdfASyntaxWalk walk, PdfPage page)
    {
        var contents = page.Dictionary.Get(KnownName.Contents);
        if (contents.AsStream() is { } single)
        {
            Queue(walk, [single], page.Resources);
        }
        else if (contents.AsArray() is { Count: > 0 } parts)
        {
            var streams = new List<PdfStream>(parts.Count);
            for (var i = 0; i < parts.Count; i++)
            {
                if (parts.Get(i).AsStream() is { } part)
                {
                    streams.Add(part);
                }
            }

            if (streams.Count > 0)
            {
                Queue(walk, [.. streams], page.Resources);
            }
        }

        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i)?.GetDictionary(KnownName.AP) is { } appearances)
            {
                QueueAppearances(walk, appearances);
            }
        }
    }

    /// <summary>Queues an annotation's normal, rollover and down appearances, including each state's.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="appearances">The appearance dictionary.</param>
    private static void QueueAppearances(PdfASyntaxWalk walk, PdfDictionary appearances)
    {
        foreach (var key in AppearanceKeys)
        {
            var entry = appearances.Get(key);
            if (entry.AsStream() is { } form)
            {
                Queue(walk, [form], form.Dictionary.GetDictionary(KnownName.Resources));
                continue;
            }

            var states = entry.AsDictionary();
            for (var i = 0; states is not null && i < states.Count; i++)
            {
                if (StoreReading.Resolve(walk.Store, states.GetValueAt(i)).AsStream() is { } state)
                {
                    Queue(walk, [state], state.Dictionary.GetDictionary(KnownName.Resources));
                }
            }
        }
    }

    /// <summary>Queues a content stream unless it has been queued before.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="parts">The stream or streams.</param>
    /// <param name="resources">The resources its names refer to.</param>
    private static void Queue(PdfASyntaxWalk walk, PdfStream[] parts, PdfDictionary? resources)
    {
        if (walk.Seen.Add(parts[0].Dictionary))
        {
            walk.Work.Push(new(parts, resources));
        }
    }

    /// <summary>Reads queued streams until none are left.</summary>
    /// <param name="walk">The walk.</param>
    private static void Drain(PdfASyntaxWalk walk)
    {
        while (walk.Work.TryPop(out var work))
        {
            walk.Scan.CancellationToken.ThrowIfCancellationRequested();
            PdfASyntaxContent.Check(walk, work);
        }
    }
}
