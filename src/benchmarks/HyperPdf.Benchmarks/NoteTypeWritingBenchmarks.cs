// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Benchmarks;

/// <summary>Measures fresh FENote creation and serialization with optional subtype attributes.</summary>
public class NoteTypeWritingBenchmarks
{
    /// <summary>Gets or sets the subtype, or null to omit the attribute.</summary>
    [Params(null, PdfNoteType.None, PdfNoteType.Footnote, PdfNoteType.Endnote)]
    public PdfNoteType? Type { get; set; }

    /// <summary>Gets or sets the number of notes created.</summary>
    [Params(1, 64)]
    public int ElementCount { get; set; }

    /// <summary>Creates a new tagged document and serializes all requested note subtypes.</summary>
    /// <returns>The file length.</returns>
    [Benchmark]
    public int WriteNotes()
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        var tags = creator.Tags!;
        var options = Type is { } type ? new PdfTagOptions { NoteType = type } : PdfTagOptions.Default;
        for (var index = 0; index < ElementCount; index++)
        {
            _ = tags.AddElement(tags.Root, "FENote", options);
        }

        return creator.ToArray().Length;
    }
}
