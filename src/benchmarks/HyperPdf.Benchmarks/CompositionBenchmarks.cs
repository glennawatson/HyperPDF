// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Composition;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdf.Benchmarks;

/// <summary>Measures composing a multi page document from styled paragraphs; allocations come from the EventPipe trace.</summary>
public class CompositionBenchmarks
{
    /// <summary>The paragraphs in the document.</summary>
    private const int ParagraphCount = 60;

    /// <summary>The body size, in points.</summary>
    private const float BodySize = 11F;

    /// <summary>The words of every paragraph.</summary>
    private const string Words = "Reading comfort depends on steady spacing, plain words and a layout that does not move. "
        + "This sentence repeats so that every paragraph wraps across several lines of the page. "
        + "The composer lays out the whole document first, then writes each page.";

    /// <summary>The blocks of the section.</summary>
    private PdfBlock[] _blocks = null!;

    /// <summary>Builds the paragraphs once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _blocks = new PdfBlock[ParagraphCount];
        for (var i = 0; i < _blocks.Length; i++)
        {
            _blocks[i] = PdfParagraph.Of(Words);
        }
    }

    /// <summary>Lays the document out and writes it.</summary>
    /// <returns>The file size.</returns>
    [Benchmark]
    public int ComposeAndWrite()
    {
        var creator = new PdfDocumentCreator(new() { Title = "Bench", Tagged = true, Language = "en" });
        var styles = PdfStyleSheet.CreateDefault(PdfFontFamily.FromBundled(creator, PdfBundledFontFamily.Sans), BodySize);
        var composition = new PdfComposition(styles, [new PdfSection(_blocks)]) { Title = "Bench" };
        _ = PdfComposer.Compose(creator, composition, null, CancellationToken.None);
        return creator.ToArray().Length;
    }
}
