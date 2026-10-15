// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Ocr;
using HyperPdf.Engine;
using HyperPdf.Ocr;

namespace HyperPdf.App.Services;

/// <summary>
/// Checks text recognition works from the app's own files, without a window: renders a built-in English sentence with
/// HyperPDF, recognises it with the shipped Tesseract and English data, and prints where each came from and what was read.
/// Installers and CI run <c>hyperpdf --check-text-recognition</c> to prove a fresh install recognises text.
/// </summary>
internal static class TextRecognitionCheck
{
    /// <summary>The command line argument that runs the check.</summary>
    internal const string Argument = "--check-text-recognition";

    /// <summary>The sentence on the sample page.</summary>
    private const string Sentence = "The quick brown fox jumps over the lazy dog";

    /// <summary>The word that must be read back.</summary>
    private const string ExpectedWord = "quick";

    /// <summary>The scanning resolution in pixels per point (300 DPI).</summary>
    private const float ScanScale = 300F / 72F;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>Runs the check.</summary>
    /// <param name="output">Receives the report.</param>
    /// <returns>0 when the sentence was read back, otherwise 1.</returns>
    internal static int Run(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var setup = OcrSetup.CreateDefault();
        using var engine = new TesseractEngine(OcrLanguageCatalog.DefaultLanguage, setup.LanguageDirectory);
        output.WriteLine($"Tesseract: {TesseractEngine.LibraryPath ?? "not found"}");
        output.WriteLine($"Language data: {engine.DataDirectory ?? "not found"}");
        if (!engine.IsAvailable)
        {
            output.WriteLine($"Text recognition is not ready: {engine.Status}.");
            return 1;
        }

        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-check-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, CreateSamplePdf());
        try
        {
            var words = Recognize(path, engine);
            var text = string.Join(' ', words.ConvertAll(static word => word.Text));
            output.WriteLine($"Read: {text}");
            return text.Contains(ExpectedWord, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Renders the sample's page at scanning resolution and recognises it.</summary>
    /// <param name="path">The sample document.</param>
    /// <param name="engine">The recogniser.</param>
    /// <returns>The words read.</returns>
    private static List<OcrWord> Recognize(string path, TesseractEngine engine)
    {
        using var document = new HyperPdfEngine().Open(path, null);
        var size = document.GetPageSizes()[0];
        var width = (int)MathF.Ceiling(size.Width * ScanScale);
        var height = (int)MathF.Ceiling(size.Height * ScanScale);
        var pixels = new byte[width * height * BytesPerPixel];
        _ = document.Render(new(0, ScanScale, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * BytesPerPixel));
        var grey = new byte[width * height];
        OcrRunner.ToGrey(pixels, grey);
        List<OcrWord> words = [];
        engine.Recognize(grey, width, height, ScanScale, words);
        return words;
    }

    /// <summary>Writes a one page PDF showing the sentence in Helvetica, which HyperPDF draws with its built-in font.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateSamplePdf()
    {
        const string content = $"BT /F1 24 Tf 36 90 Td ({Sentence}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 200] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new int[objects.Length];
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i] = pdf.Length;
            _ = pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = pdf.Length;
        _ = pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            _ = pdf.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        _ = pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
