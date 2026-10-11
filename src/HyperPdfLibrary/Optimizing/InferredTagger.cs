// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Builds a basic structure tree for an untagged document: Document, one Sect per page, then P and H1 to H6 elements
/// in the reading order <see cref = "PdfReadingStructure.InferLayout"/> finds, and a Figure for each image. Each page's
/// top-level text and image operators are wrapped in marked content with ids; the page is first marked with one id per
/// operator so the layout analysis can say which operators each paragraph holds, then marked again with the final tags.
/// Content that belongs to no element becomes an artifact. Figures get no alternative text; the report counts them.
/// The tree is marked as inferred in the XMP so checkers and people know it was not authored.
/// </summary>
[DebuggerDisplay("InferredTagger: {_figures} figures")]
internal sealed partial class InferredTagger
{
    /// <summary>The highest heading level.</summary>
    private const int MaxHeading = 6;

    /// <summary>The working copy.</summary>
    private readonly PdfDocument _document;

    /// <summary>The token for this tagging pass.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The optimiser's names.</summary>
    private readonly OptimizerNames _names;

    /// <summary>The section element of each tagged page.</summary>
    private readonly List<PdfValue> _sections = [];

    /// <summary>The parent tree's number tree entries: key, then the page's array of elements by marked content id.</summary>
    private readonly List<PdfValue> _parentTree = [];

    /// <summary>The reserved document element.</summary>
    private readonly PdfObjectId _documentElement;

    /// <summary>The figures created.</summary>
    private int _figures;

    /// <summary>The next page's parent tree key.</summary>
    private int _nextKey;

    /// <summary>Initializes a new instance of the <see cref = "InferredTagger"/> class.</summary>
    /// <param name = "document">The working copy.</param>
    /// <param name = "names">The optimiser's names.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private InferredTagger(PdfDocument document, OptimizerNames names, CancellationToken cancellationToken)
    {
        _document = document;
        _cancellationToken = cancellationToken;
        _names = names;
        _documentElement = StoreEditing.Add(document.Objects, PdfValue.Null);
    }

    /// <summary>Tags the document when it has no structure tree.</summary>
    /// <param name = "document">The working copy.</param>
    /// <param name = "names">The optimiser's names.</param>
    /// <param name = "report">Receives the changes.</param>
    /// <param name = "cancellationToken">Stops the pass between pages.</param>
    internal static void Run(PdfDocument document, OptimizerNames names, OptimizeReportBuilder report, CancellationToken cancellationToken)
    {
        if (!document.Catalog.GetRaw(KnownName.StructTreeRoot).IsNull)
        {
            report.Skip(PdfOptimizeCategory.Accessibility, 0, "The document already has a structure tree, so no tags were inferred.");
            return;
        }

        var tagger = new InferredTagger(document, names, cancellationToken);
        for (var i = 0; i < document.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tagger.TagPage(i);
        }

        if (tagger._sections.Count == 0)
        {
            report.Skip(PdfOptimizeCategory.Accessibility, 0, "No page has text or images to tag.");
            return;
        }

        tagger.FinishTree();
        InferredTagsXmp.Mark(document, report);
        report.TagsInferred = true;
        report.FiguresNeedingAltText = tagger._figures;
        report.Noted(PdfOptimizeCategory.Accessibility, 0, string.Create(CultureInfo.InvariantCulture, $"Inferred a structure tree for {tagger._sections.Count} pages from their layout."));
        if (tagger._figures > 0)
        {
            report.Warn(string.Create(CultureInfo.InvariantCulture, $"{tagger._figures} inferred figures have no alternative text yet; a person needs to describe them."));
        }

        PdfDocumentOptimizing.RefreshAfterOptimizerEdit(document);
    }

    /// <summary>Determines whether every unit has an owning element.</summary>
    /// <param name = "owners">The block owning each unit, or -1.</param>
    /// <returns><see langword="true"/> when every unit is owned.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AllOwned(int[] owners) => Array.IndexOf(owners, -1) < 0;

    /// <summary>Claims the units a node's glyphs came from that no earlier node claimed.</summary>
    /// <param name = "items">The node's glyph indices.</param>
    /// <param name = "content">The page's marked content from the probe pass.</param>
    /// <param name = "owners">The block owning each unit, or -1.</param>
    /// <param name = "block">The node's block index.</param>
    /// <returns>The units claimed, in content order.</returns>
    private static List<int> OwnUnits(List<int> items, PdfMarkedContentPage? content, int[] owners, int block)
    {
        var owned = new List<int>();
        if (content is null)
        {
            return owned;
        }

        foreach (var item in items)
        {
            var mcid = (uint)item < (uint)content.GlyphCount ? content.Glyphs[item].Mcid : -1;
            if ((uint)mcid >= (uint)owners.Length || owners[mcid] >= 0)
            {
                continue;
            }

            owners[mcid] = block;
            owned.Add(mcid);
        }

        owned.Sort();
        return owned;
    }

    /// <summary>Tags one page.</summary>
    /// <param name = "index">The page index.</param>
    private void TagPage(int index)
    {
        var store = _document.Objects;
        var page = PdfDocumentPages.GetPage(_document, index);
        var content = default(PooledBuffer);
        var marked = default(PooledBuffer);
        try
        {
            ContentExecution.DecodeContents(page, ref content, _cancellationToken);
            var units = MarkedContentRewriter.FindUnits(content.WrittenSpan, store.Names, page.Resources, store);
            if (units.Count == 0)
            {
                return;
            }

            // First pass: one id per unit, so the layout analysis reports which unit each glyph came from.
            var probe = new TagLabel[units.Count];
            for (var i = 0; i < probe.Length; i++)
            {
                probe[i] = new(_names.Span, i);
            }

            MarkedContentRewriter.Write(content.WrittenSpan, units, probe, store.Names, ref marked);
            SetContents(page, marked.WrittenSpan, -1);
            var blocks = Blocks(index, units);
            marked.Length = 0;
            var labels = Label(blocks, units.Count);
            MarkedContentRewriter.Write(content.WrittenSpan, units, labels, store.Names, ref marked);
            var key = _nextKey;
            SetContents(PdfDocumentPages.GetPage(_document, index), marked.WrittenSpan, key);
            AddSection(PdfDocumentPages.GetPage(_document, index), blocks, labels, key);
        }
        finally
        {
            content.Dispose();
            marked.Dispose();
        }
    }

    /// <summary>Replaces a page's content with one compressed stream and sets its structure keys.</summary>
    /// <param name = "page">The page.</param>
    /// <param name = "content">The new content.</param>
    /// <param name = "structParents">The page's parent tree key, or -1 while probing.</param>
    private void SetContents(PdfPage page, ReadOnlySpan<byte> content, int structParents)
    {
        var store = _document.Objects;
        var dictionary = new PdfDictionary(store, 1);
        dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
        var compressed = default(PooledBuffer);
        try
        {
            StreamRecompressor.Deflate(content, ref compressed);
            var stream = StoreEditing.Add(store, PdfValue.FromStream(new(dictionary, compressed.ToArray())));
            var copy = (StoreReading.GetObject(store, page.Id).AsDictionary() ?? page.Dictionary).Clone();
            copy.Set(KnownName.Contents, PdfValue.FromReference(stream));
            if (structParents >= 0)
            {
                copy.Set(KnownName.StructParents, PdfValue.FromInteger(structParents));
                copy.Set(KnownName.Tabs, PdfValue.FromName(KnownName.S));
            }

            StoreEditing.Replace(store, page.Id, PdfValue.FromDictionary(copy));
            PdfDocumentOptimizing.RefreshAfterOptimizerEdit(_document);
        }
        finally
        {
            compressed.Dispose();
        }
    }

    /// <summary>Groups the units into elements from the page's inferred layout, figures placed where they fall in the content.</summary>
    /// <param name = "index">The page index.</param>
    /// <param name = "units">The units.</param>
    /// <returns>The elements in reading order.</returns>
    private List<TagBlock> Blocks(int index, List<TagUnit> units)
    {
        var layout = PdfReadingStructure.InferLayout(_document, index);
        var owners = new int[units.Count];
        Array.Fill(owners, -1);
        var blocks = new List<TagBlock>();
        var items = new List<int>();
        foreach (var node in layout.Nodes)
        {
            items.Clear();
            node.CollectItems(items);
            var owned = OwnUnits(items, layout.Content, owners, blocks.Count);
            if (owned.Count > 0)
            {
                blocks.Add(new(TagFor(node), owned, false));
            }
        }

        if (!AllOwned(owners))
        {
            AddFigures(units, owners, blocks);
        }

        return blocks;
    }

    /// <summary>Gets the structure type of a layout node: a heading of its level, otherwise a paragraph.</summary>
    /// <param name = "node">The node.</param>
    /// <returns>The type.</returns>
    private PdfName TagFor(PdfSemanticNode node) => node.Role == PdfSemanticRole.Heading ? _names.Headings[Math.Clamp(node.Level, 1, MaxHeading) - 1] : KnownName.P;

    /// <summary>Adds a figure for each image unit, after the element whose content comes before it.</summary>
    /// <param name = "units">The units.</param>
    /// <param name = "owners">The block owning each unit, or -1.</param>
    /// <param name = "blocks">The elements, in reading order.</param>
    private void AddFigures(List<TagUnit> units, int[] owners, List<TagBlock> blocks)
    {
        for (var unit = 0; unit < units.Count; unit++)
        {
            if (units[unit].Kind != TagUnitKind.Figure || owners[unit] >= 0)
            {
                continue;
            }

            var position = 0;
            for (var b = 0; b < blocks.Count; b++)
            {
                position = blocks[b].FirstUnit < unit ? b + 1 : position;
            }

            owners[unit] = blocks.Count;
            blocks.Insert(position, new(_names.Figure, [unit], true));
        }
    }

    /// <summary>Gives each owned unit its final tag and a marked content id in reading order; the rest become artifacts.</summary>
    /// <param name = "blocks">The elements, in reading order.</param>
    /// <param name = "count">The number of units.</param>
    /// <returns>The labels by unit.</returns>
    private TagLabel[] Label(List<TagBlock> blocks, int count)
    {
        var labels = new TagLabel[count];
        Array.Fill(labels, new(_names.Span, -1));
        var mcid = 0;
        foreach (var block in blocks)
        {
            foreach (var unit in block.Units)
            {
                labels[unit] = new(block.Tag, mcid);
                mcid++;
            }
        }

        return labels;
    }
}
