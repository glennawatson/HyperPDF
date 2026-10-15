// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Buffers;
using System.Globalization;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Builds a basic structure tree for an untagged document: Document, one Sect per page, then P and H1 to H6 elements
/// in the reading order <see cref = "PdfReadingStructure.InferLayout(PdfDocument, int, PdfLayoutInference, CancellationToken)"/> finds, and a Figure for each image. Each page's
/// top-level text and image operators are wrapped in marked content with ids; the page is first marked with one id per
/// operator so the layout analysis can say which operators each paragraph holds, then marked again with the final tags.
/// Content that belongs to no element becomes an artifact. Figures get no alternative text; the report counts them.
/// The tree is marked as inferred in the XMP so checkers and people know it was not authored.
/// </summary>
internal static class InferredTagger
{
    /// <summary>Tags the document when it has no structure tree.</summary>
    /// <param name = "document">The working copy.</param>
    /// <param name = "options">The optimiser's options: the language guess and the figure description hook.</param>
    /// <param name = "report">Receives the changes.</param>
    /// <param name="scratchPools">The document or registered service's compression owner.</param>
    /// <param name = "cancellationToken">Stops the pass between pages.</param>
    internal static void Run(PdfDocument document, in PdfOptimizeOptions options, OptimizeReportBuilder report, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        if (!document.Catalog.GetRaw(KnownName.StructTreeRoot).IsNull)
        {
            report.Skip(PdfOptimizeCategory.Accessibility, 0, "The document already has a structure tree, so no tags were inferred.");
            return;
        }

        var inference = new PdfLayoutInference { Pagination = PdfPaginationProfile.Find(document, cancellationToken), FigureAltText = options.FigureAltText, GuessLanguage = options.GuessLanguage };
        using var state = TaggerState.Rent(document, inference, scratchPools, cancellationToken);
        for (var i = 0; i < document.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TagPage(state, i);
        }

        if (state.SectionCount == 0)
        {
            report.Skip(PdfOptimizeCategory.Accessibility, 0, "No page has text or images to tag.");
            return;
        }

        TagStructure.FinishTree(state);
        InferredTagsXmp.Mark(document, report);
        report.TagsInferred = true;
        report.FiguresNeedingAltText = state.Figures;
        report.Noted(PdfOptimizeCategory.Accessibility, 0, string.Create(CultureInfo.InvariantCulture, $"Inferred a structure tree for {state.SectionCount} pages from their layout."));
        if (state.Figures > 0)
        {
            report.Warn(string.Create(CultureInfo.InvariantCulture, $"{state.Figures} inferred figures have no alternative text yet; a person needs to describe them."));
        }

        PdfDocumentOptimizing.RefreshAfterOptimizerEdit(document);
    }

    /// <summary>Gives the units of the elements their tags and marked content ids, in reading order.</summary>
    /// <param name = "build">The elements.</param>
    /// <param name = "labels">The labels by unit; the units no element owns stay artifacts.</param>
    private static void Assign(ref TagBuild build, Span<TagLabel> labels)
    {
        var mcid = 0;
        foreach (var node in build.Nodes)
        {
            for (var i = 0; i < node.UnitCount; i++)
            {
                labels[build.OwnedUnits[node.UnitStart + i]] = new(node.Tag, mcid);
                mcid++;
            }
        }
    }

    /// <summary>Tags one page.</summary>
    /// <param name = "state">The pass.</param>
    /// <param name = "index">The page index.</param>
    private static void TagPage(TaggerState state, int index)
    {
        var store = state.Document.Objects;
        var page = PdfDocumentPages.GetPage(state.Document, index);
        var content = default(PooledBuffer);
        var marked = default(PooledBuffer);
        var units = default(RentedList<TagUnit>);
        var labels = default(RentedList<TagLabel>);
        try
        {
            ContentExecution.DecodeContents(page, ref content, state.CancellationToken);
            MarkedContentRewriter.FindUnits(content.WrittenSpan, store.Names, page.Resources, store, ref units);
            if (units.Count == 0)
            {
                return;
            }

            // First pass: one id per unit, so the layout analysis reports which unit each glyph came from.
            for (var i = 0; i < units.Count; i++)
            {
                labels.Add(new(OptimizerNames.Span, units[i].Kind == TagUnitKind.Path ? -1 : i));
            }

            MarkedContentRewriter.Write(content.WrittenSpan, units.Span, labels.Span, store.Names, ref marked);
            SetContents(state, page, marked.WrittenSpan, -1);
            marked.Length = 0;
            TagUnits(state, index, units.Span, ref labels, content.WrittenSpan, ref marked);
        }
        finally
        {
            content.Dispose();
            marked.Dispose();
            units.Dispose();
            labels.Dispose();
        }
    }

    /// <summary>Groups a page's units into elements from its inferred layout, marks them and writes the page's section.</summary>
    /// <param name = "state">The pass.</param>
    /// <param name = "index">The page index.</param>
    /// <param name = "units">The units.</param>
    /// <param name = "labels">Holds the probe labels; refilled with the final ones.</param>
    /// <param name = "content">The page's original content.</param>
    /// <param name = "marked">Receives the marked content.</param>
    private static void TagUnits(TaggerState state, int index, ReadOnlySpan<TagUnit> units, ref RentedList<TagLabel> labels, ReadOnlySpan<byte> content, ref PooledBuffer marked)
    {
        var store = state.Document.Objects;
        var layout = PdfReadingStructure.InferLayout(state.Document, index, state.Inference, state.CancellationToken);
        var owners = ArrayPool<int>.Shared.Rent(units.Length);
        owners.AsSpan(0, units.Length).Fill(-1);
        var build = new TagBuild(layout.Content, units, owners.AsSpan(0, units.Length), state.Scratch);
        try
        {
            TagBlocks.Build(layout, ref build, state.Inference.GuessLanguage);
            if (!TagBlocks.AllOwned(build.Owners))
            {
                TagBlocks.AddUnclaimedFigures(ref build);
            }

            labels.Clear();
            for (var i = 0; i < units.Length; i++)
            {
                labels.Add(new(OptimizerNames.Span, -1));
            }

            Assign(ref build, labels.Span);
            MarkedContentRewriter.Write(content, units, labels.Span, store.Names, ref marked);
            var key = state.NextKey;
            SetContents(state, PdfDocumentPages.GetPage(state.Document, index), marked.WrittenSpan, key);
            TagStructure.AddSection(state, PdfDocumentPages.GetPage(state.Document, index), ref build, labels.Span, key);
        }
        finally
        {
            build.Scratch.Clear();
            build.Dispose();
            ArrayPool<int>.Shared.Return(owners);
        }
    }

    /// <summary>Replaces a page's content with one compressed stream and sets its structure keys.</summary>
    /// <param name = "state">The pass.</param>
    /// <param name = "page">The page.</param>
    /// <param name = "content">The new content.</param>
    /// <param name = "structParents">The page's parent tree key, or -1 while probing.</param>
    private static void SetContents(TaggerState state, PdfPage page, ReadOnlySpan<byte> content, int structParents)
    {
        var store = state.Document.Objects;
        var dictionary = new PdfDictionary(store, 1);
        dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
        var compressed = default(PooledBuffer);
        try
        {
            StreamRecompressor.Deflate(content, ref compressed, state.ScratchPools);
            var stream = StoreEditing.Add(store, PdfValue.FromStream(new(dictionary, compressed.ToArray())));
            var copy = (StoreReading.GetObject(store, page.Id).AsDictionary() ?? page.Dictionary).Clone();
            copy.Set(KnownName.Contents, PdfValue.FromReference(stream));
            if (structParents >= 0)
            {
                copy.Set(KnownName.StructParents, PdfValue.FromInteger(structParents));
                copy.Set(KnownName.Tabs, PdfValue.FromName(KnownName.S));
            }

            StoreEditing.Replace(store, page.Id, PdfValue.FromDictionary(copy));
            PdfDocumentOptimizing.RefreshAfterOptimizerEdit(state.Document);
        }
        finally
        {
            compressed.Dispose();
        }
    }
}
