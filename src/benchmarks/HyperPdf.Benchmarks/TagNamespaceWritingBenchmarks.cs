// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Creation;

namespace HyperPdf.Benchmarks;

/// <summary>Measures fresh unclaimed PDF 2.0 creation and serialization with standard structure namespaces.</summary>
public class TagNamespaceWritingBenchmarks
{
    /// <summary>The case combining legacy notes with a PDF 2.0-only element.</summary>
    private const string MixedTypes = "Mixed";

    /// <summary>The standard PDF 2.0 footnote or endnote spelling.</summary>
    private const string FootnoteType = "FENote";

    /// <summary>Gets or sets the standard spelling, or Mixed for legacy notes alongside a PDF 2.0 element.</summary>
    [Params("P", "Note", FootnoteType, MixedTypes)]
    public string Type { get; set; } = "P";

    /// <summary>Gets or sets the number of structure elements written.</summary>
    [Params(1, 64)]
    public int ElementCount { get; set; }

    /// <summary>Constructs and serializes a fresh tagged document, including its metadata and output array.</summary>
    /// <returns>The file length.</returns>
    [Benchmark]
    public int WriteDocument()
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        var tags = creator.Tags!;
        var type = Type == MixedTypes ? "Note" : Type;
        for (var index = 0; index < ElementCount; index++)
        {
            _ = tags.AddElement(tags.Root, type);
        }

        if (Type == MixedTypes)
        {
            _ = tags.AddElement(tags.Root, FootnoteType);
        }

        return creator.ToArray().Length;
    }
}
