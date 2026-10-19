// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Benchmarks;

/// <summary>Measures table placement with spans inside and beyond the grid budget.</summary>
public class TableHeaderResolverBenchmarks
{
    /// <summary>The prepared table, reused without accumulating header links.</summary>
    private PdfSemanticNode _table = null!;

    /// <summary>Gets or sets the column span.</summary>
    [Params(1, 257, 1_048_576)]
    public int Span { get; set; }

    /// <summary>Prepares one cell with no header links to accumulate between operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _table = new(PdfSemanticRole.Table, PdfNodeOrigin.Tagged, 0);
        var row = new PdfSemanticNode(PdfSemanticRole.TableRow, PdfNodeOrigin.Tagged, 0);
        var element = new PdfStructureElement(new PdfDictionary(null), default, null) { Attributes = new(string.Empty, null, PdfTableScope.None, [], 1, Span, string.Empty, string.Empty) };
        row.Add(new(PdfSemanticRole.TableCell, PdfNodeOrigin.Tagged, 0) { Element = element });
        _table.Add(row);
    }

    /// <summary>Places the prepared cell using fresh operation-local working storage.</summary>
    /// <returns>The resulting column span.</returns>
    [Benchmark]
    public int Resolve()
    {
        TableHeaderResolver.Resolve(_table);
        return _table.Children[0].Children[0].ColumnSpan;
    }
}
