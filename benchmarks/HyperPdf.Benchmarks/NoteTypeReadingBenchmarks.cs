// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures fresh structure construction for FENote attributes.</summary>
public class NoteTypeReadingBenchmarks
{
    /// <summary>The first note's object number.</summary>
    private const int FirstNoteObject = 5;

    /// <summary>The document structure element's object number.</summary>
    private const int DocumentObject = 4;

    /// <summary>The structure tree root's object number.</summary>
    private const int StructureRootObject = 3;

    /// <summary>The input bytes, reused without caching a parsed document or tree.</summary>
    private byte[] _bytes = [];

    /// <summary>Gets or sets the number of notes in the structure tree.</summary>
    [Params(1, 64)]
    public int NoteCount { get; set; }

    /// <summary>Builds the input outside the measured fresh-document read.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var namespaceObject = FirstNoteObject + NoteCount;
        var objects = new string[namespaceObject];
        var kids = new StringBuilder();
        objects[0] = "<< /Type /Catalog /Version /2.0 /Pages 2 0 R /StructTreeRoot 3 0 R >>";
        objects[1] = "<< /Type /Pages /Count 0 /Kids [] >>";
        for (var index = 0; index < NoteCount; index++)
        {
            var id = FirstNoteObject + index;
            _ = kids.Append(CultureInfo.InvariantCulture, $"{id} 0 R ");
            objects[id - 1] = string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /StructElem /S /FENote /P 4 0 R /NS {namespaceObject} 0 R /A << /O /FENote /NoteType /Footnote >> >>");
        }

        objects[DocumentObject - 1] = string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /StructElem /S /Document /P 3 0 R /NS {namespaceObject} 0 R /K [{kids}] >>");
        objects[StructureRootObject - 1] = string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /StructTreeRoot /K 4 0 R /Namespaces [{namespaceObject} 0 R] >>");
        objects[namespaceObject - 1] = "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>";
        _bytes = MiniPdf.Build(objects);
    }

    /// <summary>Opens and disposes a fresh document and constructs its structure tree.</summary>
    /// <returns>The number of parsed note elements.</returns>
    [Benchmark]
    public int ReadStructure()
    {
        using var document = PdfDocumentReader.Open(_bytes, null);
        return PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids.Count;
    }
}
