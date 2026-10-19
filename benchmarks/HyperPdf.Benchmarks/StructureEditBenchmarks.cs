// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Benchmarks;

/// <summary>Measures one tag edit and its undo on a small tagged document, including the structure tree read the edit leads to.</summary>
public class StructureEditBenchmarks
{
    /// <summary>The description written by the text edit.</summary>
    private const string Description = "A thick black bar";

    /// <summary>The figure's position among the document element's children.</summary>
    private const int FigurePosition = 3;

    /// <summary>The open document.</summary>
    private PdfDocument _document = null!;

    /// <summary>Opens the tagged sample.</summary>
    [GlobalSetup]
    public void Setup() => _document = PdfDocumentReader.Open(TestPdf.CreateTagged(), null);

    /// <summary>Releases the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Sets a description on the figure and takes it back.</summary>
    /// <returns>Whether the edit was undone.</returns>
    [Benchmark]
    public bool SetDescriptionAndUndo()
    {
        PdfDocumentStructureEditing.SetText(_document, Figure(), PdfStructureTextField.AlternateText, Description);
        return PdfDocumentEditing.Undo(_document);
    }

    /// <summary>Changes the heading's level and takes it back.</summary>
    /// <returns>Whether the edit was undone.</returns>
    [Benchmark]
    public bool SetTypeAndUndo()
    {
        PdfDocumentStructureEditing.SetType(_document, Heading(), "H2");
        return PdfDocumentEditing.Undo(_document);
    }

    /// <summary>Moves a paragraph to the end of its group and takes it back.</summary>
    /// <returns>Whether the edit was undone.</returns>
    [Benchmark]
    public bool MoveAndUndo()
    {
        _ = PdfDocumentStructureEditing.Move(_document, Paragraph(), 1);
        return PdfDocumentEditing.Undo(_document);
    }

    /// <summary>Gets the figure.</summary>
    /// <returns>The element.</returns>
    private PdfStructureElement Figure() => Kid(FigurePosition);

    /// <summary>Gets the heading.</summary>
    /// <returns>The element.</returns>
    private PdfStructureElement Heading() => Kid(0);

    /// <summary>Gets the first paragraph.</summary>
    /// <returns>The element.</returns>
    private PdfStructureElement Paragraph() => Kid(1);

    /// <summary>Gets a child of the document element.</summary>
    /// <param name="index">The child's position.</param>
    /// <returns>The element.</returns>
    private PdfStructureElement Kid(int index) => PdfDocumentTagged.GetStructureTree(_document)!.Roots[0].Kids[index].Element!;
}
