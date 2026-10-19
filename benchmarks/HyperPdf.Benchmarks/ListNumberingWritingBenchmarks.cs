// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Benchmarks;

/// <summary>Measures fresh list creation and serialization with optional numbering attributes.</summary>
public class ListNumberingWritingBenchmarks
{
    /// <summary>The side length of the blank page.</summary>
    private const float PageSide = 300F;

    /// <summary>Gets or sets the requested numbering name, or null to omit it.</summary>
    [Params(null, PdfListNumbering.Ordered)]
    public PdfListNumbering? Numbering { get; set; }

    /// <summary>Gets or sets the number of independent lists.</summary>
    [Params(1, 64)]
    public int ElementCount { get; set; }

    /// <summary>Creates lists with an item body and serializes the complete document.</summary>
    /// <returns>The file length.</returns>
    [Benchmark]
    public int WriteLists()
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        var tags = creator.Tags!;
        var options = Numbering is { } numbering ? new PdfTagOptions { ListNumbering = numbering } : PdfTagOptions.Default;
        for (var index = 0; index < ElementCount; index++)
        {
            var list = tags.AddElement(tags.Root, "L", options);
            var item = tags.AddElement(list, "LI");
            _ = tags.AddElement(item, "LBody");
        }

        using (var page = creator.BeginPage(PageSide, PageSide))
        {
            _ = page.Finish();
        }

        return creator.ToArray().Length;
    }
}
