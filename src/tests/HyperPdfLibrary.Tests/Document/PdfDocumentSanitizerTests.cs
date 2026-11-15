// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Checks that hidden information is taken out of a document: scripts, file attachments and comments.</summary>
public sealed class PdfDocumentSanitizerTests
{
    /// <summary>The scripts the fixture holds: the open action, one named script, one on the page and one on the link.</summary>
    private const int ScriptCount = 4;

    /// <summary>The marks taken out when only comments are removed: the note and the file attachment note, which is not a link or a form field.</summary>
    private const int TwoMarks = 2;

    /// <summary>A page with a script on opening, a note, a link that runs a script and a file attachment note.</summary>
    private static readonly string[] Objects =
    [
        "<< /Type /Catalog /Pages 2 0 R /OpenAction << /S /JavaScript /JS (app.alert\\(1\\)) >> /Names << /JavaScript << /Names [(a) << /S /JavaScript /JS (x) >>] >> >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /AA << /O << /S /JavaScript /JS (y) >> >> /Annots [4 0 R 5 0 R 6 0 R] >>",
        "<< /Type /Annot /Subtype /Text /Rect [10 10 30 30] /Contents (note) >>",
        "<< /Type /Annot /Subtype /Link /Rect [40 40 60 60] /A << /S /JavaScript /JS (z) >> >>",
        "<< /Type /Annot /Subtype /FileAttachment /Rect [70 70 90 90] /Contents (file) >>",
    ];

    /// <summary>Removing everything counts what was taken out and leaves the link without its script.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EverythingChosenIsTakenOut()
    {
        using var document = PdfDocumentReader.Open(MiniPdf.Build(Objects), null);

        var report = PdfDocumentSanitizer.Sanitize(document, PdfSanitizeOptions.All);
        var page = PdfDocumentPages.GetPage(document, 0);
        var notes = PdfPageAnnotations.GetArray(document.Objects, page)!;

        await Assert.That(report.Scripts).IsEqualTo(ScriptCount);
        await Assert.That(report.Comments).IsEqualTo(1);
        await Assert.That(report.Attachments).IsEqualTo(1);
        await Assert.That(document.Catalog.ContainsKey(KnownName.OpenAction)).IsFalse();
        await Assert.That(page.Dictionary.ContainsKey(KnownName.AA)).IsFalse();
        await Assert.That(notes.Count).IsEqualTo(1);
        await Assert.That(notes.GetDictionary(0)!.ContainsKey(KnownName.A)).IsFalse();
    }

    /// <summary>Only what the options name is taken out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyWhatIsChosenIsTakenOut()
    {
        using var document = PdfDocumentReader.Open(MiniPdf.Build(Objects), null);

        var report = PdfDocumentSanitizer.Sanitize(document, new(false, false, false, true));
        var notes = PdfPageAnnotations.GetArray(document.Objects, PdfDocumentPages.GetPage(document, 0))!;

        await Assert.That(report.Comments).IsEqualTo(TwoMarks);
        await Assert.That(report.Scripts).IsEqualTo(0);
        await Assert.That(notes.Count).IsEqualTo(1);
        await Assert.That(document.Catalog.ContainsKey(KnownName.OpenAction)).IsTrue();
    }
}
