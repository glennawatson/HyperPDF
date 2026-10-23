// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.XObjects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures reading an image's alternates, choosing the print version, and reading a form XObject's external reference.</summary>
public class HyperPdfXObjectReadBenchmarks
{
    /// <summary>The object number of the main image.</summary>
    private const int ImageNumber = 4;

    /// <summary>The object number of the reference form.</summary>
    private const int FormNumber = 7;

    /// <summary>The entries every sample image has apart from its alternates.</summary>
    private const string ImageEntries = "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8";

    /// <summary>The fixture.</summary>
    private PdfDocument? _document;

    /// <summary>The main image.</summary>
    private PdfStream? _image;

    /// <summary>The reference form.</summary>
    private PdfStream? _form;

    /// <summary>Opens a document with an image that has three alternates and a form with a reference.</summary>
    /// <exception cref="InvalidOperationException">The fixture does not read as expected.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            MiniPdf.Stream($"{ImageEntries} /Alternates [<< /Image 5 0 R >> << /Image 6 0 R /DefaultForPrinting true >> << /Image 5 0 R >>]", "m"),
            MiniPdf.Stream(ImageEntries, "a"),
            MiniPdf.Stream(ImageEntries, "b"),
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Ref << /F (other.pdf) /Page 2 /ID [(AAAA) (BBBB)] >>", "0 g 0 0 10 10 re f"));
        _document = PdfDocumentReader.Open(bytes, null);
        _image = StoreReading.GetObject(_document.Objects, new(ImageNumber, 0)).AsStream();
        _form = StoreReading.GetObject(_document.Objects, new(FormNumber, 0)).AsStream();
        if (_image is null || _form is null || PdfDocumentXObjects.GetImageAlternates(_document, _image).Length != 2 || PdfDocumentXObjects.GetReference(_document, _form) is null)
        {
            throw new InvalidOperationException("The XObject benchmark fixture did not read as expected.");
        }
    }

    /// <summary>Closes the fixture.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Chooses the image for screen, which is always the main image.</summary>
    /// <returns>The raw length of the chosen image.</returns>
    [Benchmark(Baseline = true)]
    public int SelectForScreen() => PdfDocumentXObjects.SelectImage(_document!, _image!, PdfImageUse.Screen).RawLength;

    /// <summary>Reads the alternates list.</summary>
    /// <returns>The number of alternates.</returns>
    [Benchmark]
    public int ReadAlternates() => PdfDocumentXObjects.GetImageAlternates(_document!, _image!).Length;

    /// <summary>Chooses the image for printing, which reads the alternates to find the printing default.</summary>
    /// <returns>The raw length of the chosen image.</returns>
    [Benchmark]
    public int SelectForPrint() => PdfDocumentXObjects.SelectImage(_document!, _image!, PdfImageUse.Print).RawLength;

    /// <summary>Reads the form's external reference.</summary>
    /// <returns>The length of the file name.</returns>
    [Benchmark]
    public int ReadReference() => PdfDocumentXObjects.GetReference(_document!, _form!)!.FileName.Length;
}
