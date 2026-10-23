// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Appends annotations to one page one at a time, as importing form data or a markup file does, to show how the cost grows with their number.</summary>
public class HyperPdfAnnotationAppendBenchmarks
{
    /// <summary>The PDF bytes of a one page document.</summary>
    private byte[] _pdf = [];

    /// <summary>Gets or sets the annotations appended.</summary>
    [Params(200, 800)]
    public int Annotations { get; set; }

    /// <summary>Builds the document bytes outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup() => _pdf = TestPdf.Create(1);

    /// <summary>Opens a one page document and appends the annotations.</summary>
    /// <returns>The index of the last annotation.</returns>
    [Benchmark]
    public int AppendOneByOne()
    {
        using var document = PdfDocumentReader.Open(_pdf, null);
        var page = PdfDocumentPages.GetPage(document, 0);
        var last = 0;
        for (var i = 0; i < Annotations; i++)
        {
            var annotation = new PdfDictionary(document.Objects);
            annotation.Add(KnownName.F, PdfValue.FromInteger(i));
            last = PdfPageAnnotations.Append(document.Objects, page, annotation);
        }

        return last;
    }
}
