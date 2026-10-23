// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Creation;

namespace HyperPdf.Benchmarks;

/// <summary>Measures fresh non-interactive field creation and complete document serialization.</summary>
public class PrintFieldWritingBenchmarks
{
    /// <summary>The side length of the blank page.</summary>
    private const float PageSide = 300F;

    /// <summary>Gets or sets whether to request checkbox attributes.</summary>
    [Params(false, true)]
    public bool Attributes { get; set; }

    /// <summary>Gets or sets the number of non-interactive fields.</summary>
    [Params(1, 64)]
    public int FieldCount { get; set; }

    /// <summary>Creates Form elements and serializes the complete document.</summary>
    /// <returns>The file length.</returns>
    [Benchmark]
    public int WriteFields()
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        var tags = creator.Tags!;
        var options = Attributes ? new PdfTagOptions { PrintField = new("cb", "on", "Accept terms") } : PdfTagOptions.Default;
        for (var index = 0; index < FieldCount; index++)
        {
            _ = tags.AddElement(tags.Root, "Form", options);
        }

        using (var page = creator.BeginPage(PageSide, PageSide))
        {
            _ = page.Finish();
        }

        return creator.ToArray().Length;
    }
}
