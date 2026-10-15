// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The parameters of the symbol dictionary decoding procedure (T.88 table 13).</summary>
[DebuggerDisplay("Jbig2SymbolDictionarySettings: {NewCount} new, {ExportCount} exported")]
internal sealed class Jbig2SymbolDictionarySettings
{
    /// <summary>Gets or sets a value indicating whether the dictionary is Huffman coded, SDHUFF.</summary>
    internal bool Huffman { get; set; }

    /// <summary>Gets or sets a value indicating whether symbols are refined or aggregated, SDREFAGG.</summary>
    internal bool RefinementAggregate { get; set; }

    /// <summary>Gets or sets the generic template, SDTEMPLATE.</summary>
    internal int Template { get; set; }

    /// <summary>Gets or sets the refinement template, SDRTEMPLATE.</summary>
    internal int RefinementTemplate { get; set; }

    /// <summary>Gets or sets the generic adaptive template pixels, SDAT.</summary>
    internal Jbig2AtPixels At { get; set; }

    /// <summary>Gets or sets the refinement adaptive template pixels, SDRAT.</summary>
    internal Jbig2AtPixels RefinementAt { get; set; }

    /// <summary>Gets or sets the number of symbols exported, SDNUMEXSYMS.</summary>
    internal long ExportCount { get; set; }

    /// <summary>Gets or sets the number of symbols defined, SDNUMNEWSYMS.</summary>
    internal int NewCount { get; set; }

    /// <summary>Gets or sets the height class delta table, SDHUFFDH.</summary>
    internal Jbig2HuffmanTable HeightDelta { get; set; } = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B4);

    /// <summary>Gets or sets the width delta table, SDHUFFDW.</summary>
    internal Jbig2HuffmanTable WidthDelta { get; set; } = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B2);

    /// <summary>Gets or sets the collective bitmap size table, SDHUFFBMSIZE.</summary>
    internal Jbig2HuffmanTable BitmapSize { get; set; } = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B1);

    /// <summary>Gets or sets the aggregate instance count table, SDHUFFAGGINST.</summary>
    internal Jbig2HuffmanTable AggregateCount { get; set; } = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B1);

    /// <summary>Gets or sets a value indicating whether the contexts of the last referred dictionary are reused.</summary>
    internal bool ReuseContexts { get; set; }

    /// <summary>Gets or sets a value indicating whether the contexts are kept for a later dictionary.</summary>
    internal bool RetainContexts { get; set; }

    /// <summary>Sets every parameter to the value a new dictionary starts with.</summary>
    internal void Reset()
    {
        Huffman = false;
        RefinementAggregate = false;
        Template = 0;
        RefinementTemplate = 0;
        At = default;
        RefinementAt = default;
        ExportCount = 0;
        NewCount = 0;
        HeightDelta = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B4);
        WidthDelta = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B2);
        BitmapSize = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B1);
        AggregateCount = Jbig2HuffmanTable.GetStandard(Jbig2StandardTable.B1);
        ReuseContexts = false;
        RetainContexts = false;
    }
}
