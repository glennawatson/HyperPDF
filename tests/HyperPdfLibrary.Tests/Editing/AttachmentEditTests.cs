// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.AssociatedFiles;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Tests.Conformance;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for <see cref="PdfDocumentAttachmentEditing"/> and <see cref="PdfDocumentFileAttachmentAnnotations"/>.</summary>
public sealed class AttachmentEditTests
{
    /// <summary>Pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>Attachments added to the long list.</summary>
    private const int ManyFiles = 120;

    /// <summary>The third part of ISO 19005.</summary>
    private const int PdfAPart3 = 3;

    /// <summary>The year used for dates.</summary>
    private const int Year = 2026;

    /// <summary>The month used for dates.</summary>
    private const int Month = 5;

    /// <summary>The day used for dates.</summary>
    private const int Day = 6;

    /// <summary>The hour used for dates.</summary>
    private const int Hour = 7;

    /// <summary>The third of every three attachments is removed.</summary>
    private const int Third = 3;

    /// <summary>The description of the sample file.</summary>
    private const string ReportText = "The report";

    /// <summary>The name the sample file is renamed to.</summary>
    private const string RenamedFile = "notes.txt";

    /// <summary>The left edge of the annotation icon.</summary>
    private const float IconLeft = 100;

    /// <summary>The bottom edge of the annotation icon.</summary>
    private const float IconBottom = 600;

    /// <summary>The right edge of the annotation icon.</summary>
    private const float IconRight = 120;

    /// <summary>The top edge of the annotation icon.</summary>
    private const float IconTop = 620;

    /// <summary>The first coordinate of the small icon.</summary>
    private const float SmallNear = 10;

    /// <summary>The second coordinate of the small icon.</summary>
    private const float SmallFar = 30;

    /// <summary>The MIME type used for the sample file.</summary>
    private const string Mime = "application/pdf";

    /// <summary>The name used for the sample file.</summary>
    private const string FileName = "report.pdf";

    /// <summary>The bytes of the sample file; the text is unique so the tests can look for it in saved files.</summary>
    private static readonly byte[] Content = "ATTACHMENT-BODY-4F2A9C71 attached bytes ATTACHMENT-BODY-4F2A9C71"u8.ToArray();

    /// <summary>Gets a date the tests write.</summary>
    private static DateTimeOffset Date => new(Year, Month, Day, Hour, 0, 0, TimeSpan.Zero);

    /// <summary>An added file records its MIME type with an escaped slash, its size, dates and MD5 checksum, and reopens the same.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddedFileRecordsItsParameters(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);

        _ = await PdfDocumentAttachmentEditing.AddAttachmentAsync(document, Sample(), cancellationToken);

        var incremental = EditChecks.SaveIncremental(document);
        var compact = EditChecks.SaveCompact(document);
        await Assert.That(Encoding.Latin1.GetString(incremental)).Contains("/Subtype /application#2Fpdf");
        foreach (var file in new[] { incremental, compact })
        {
            using var reopened = PdfDocumentReader.Open(file, null);
            var attachment = PdfDocumentAttachments.GetAttachments(reopened).Single();
            await Assert.That(attachment.Key).IsEqualTo(FileName);
            await Assert.That(attachment.Name).IsEqualTo(FileName);
            await Assert.That(attachment.MimeType).IsEqualTo(Mime);
            await Assert.That(attachment.Description).IsEqualTo(ReportText);
            await Assert.That(attachment.Relationship).IsEqualTo(PdfFileRelationship.Data);
            await Assert.That(attachment.Size).IsEqualTo(Content.Length);
            await Assert.That(attachment.CheckSum).IsEquivalentTo(Hash(Content));
            await Assert.That(attachment.Created).IsEqualTo(Date);
            await Assert.That(attachment.Modified).IsEqualTo(Date.AddHours(1));
            await Assert.That(attachment.Data!.DecodeToArray(CancellationToken.None)).IsEquivalentTo(Content);
        }

        await EditChecks.AssertQpdfCleanAsync("attachment-incremental", incremental, cancellationToken);
        await EditChecks.AssertQpdfCleanAsync("attachment-compact", compact, cancellationToken);
        var attachments = await Writing.QpdfJson.ReadAsync(compact, "attachments", cancellationToken);
        if (attachments is not { } json)
        {
            return;
        }

        await Assert.That(json.GetProperty(FileName).GetProperty("preferredname").GetString()).IsEqualTo(FileName);
        await Assert.That(json.GetProperty(FileName).GetProperty("description").GetString()).IsEqualTo(ReportText);
    }

    /// <summary>Changing an attachment updates the specification and stream, and undo puts each change back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangesAreAppliedAndUndone()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        _ = PdfDocumentAttachmentEditing.AddAttachment(document, Sample());
        var replacement = "new bytes"u8.ToArray();

        var found = await PdfDocumentAttachmentEditing.UpdateAttachmentAsync(
            document,
            FileName,
            new() { Description = "Renamed", MimeType = "text/plain", Relationship = PdfFileRelationship.Source, Data = replacement, Name = RenamedFile },
            CancellationToken.None);

        await Assert.That(found).IsTrue();
        var attachment = PdfDocumentAttachments.GetAttachments(document).Single();
        await Assert.That(attachment.Key).IsEqualTo(RenamedFile);
        await Assert.That(attachment.Name).IsEqualTo(RenamedFile);
        await Assert.That(attachment.Description).IsEqualTo("Renamed");
        await Assert.That(attachment.MimeType).IsEqualTo("text/plain");
        await Assert.That(attachment.Relationship).IsEqualTo(PdfFileRelationship.Source);
        await Assert.That(attachment.Size).IsEqualTo(replacement.Length);
        await Assert.That(attachment.CheckSum).IsEquivalentTo(Hash(replacement));
        await Assert.That(attachment.Data!.DecodeToArray(CancellationToken.None)).IsEquivalentTo(replacement);
        await Assert.That(await PdfDocumentAttachmentEditing.UpdateAttachmentAsync(document, "absent", new() { Description = "x" }, CancellationToken.None)).IsFalse();

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        var restored = PdfDocumentAttachments.GetAttachments(document).Single();
        await Assert.That(restored.Key).IsEqualTo(FileName);
        await Assert.That(restored.MimeType).IsEqualTo(Mime);
        await Assert.That(restored.Data!.DecodeToArray(CancellationToken.None)).IsEquivalentTo(Content);
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentAttachments.GetAttachments(document)).IsEmpty();
    }

    /// <summary>Removing an attachment deletes its objects, and a compact save no longer holds the bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovedFileIsGoneFromSavedFiles()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var id = PdfDocumentAttachmentEditing.AddAttachment(document, Sample() with { Compress = false });
        var added = EditChecks.SaveReadable(document);
        await Assert.That(Encoding.Latin1.GetString(added)).Contains("ATTACHMENT-BODY-4F2A9C71");
        using var reopened = PdfDocumentReader.Open(added, null);

        await Assert.That(await PdfDocumentAttachmentEditing.RemoveAttachmentAsync(reopened, FileName, CancellationToken.None)).IsTrue();
        await Assert.That(await PdfDocumentAttachmentEditing.RemoveAttachmentAsync(reopened, FileName, CancellationToken.None)).IsFalse();

        await Assert.That(PdfDocumentAttachments.GetAttachments(reopened)).IsEmpty();
        await Assert.That(reopened.Catalog.GetDictionary(KnownName.Names)).IsNull();
        await Assert.That(Encoding.Latin1.GetString(EditChecks.SaveReadable(reopened))).DoesNotContain("ATTACHMENT-BODY-4F2A9C71");
        await Assert.That(StoreReading.GetObject(document.Objects, id).IsNull).IsFalse();
    }

    /// <summary>Many attachments keep the name tree balanced, and removing some keeps it so.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ManyAttachmentsKeepTheTreeBalanced(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        for (var i = 0; i < ManyFiles; i++)
        {
            _ = PdfDocumentAttachmentEditing.AddAttachment(document, new(string.Create(CultureInfo.InvariantCulture, $"file-{i:D4}.txt"), Content));
        }

        for (var i = 0; i < ManyFiles; i += Third)
        {
            await Assert.That(PdfDocumentAttachmentEditing.RemoveAttachment(document, string.Create(CultureInfo.InvariantCulture, $"file-{i:D4}.txt"))).IsTrue();
        }

        var root = document.Catalog.GetDictionary(KnownName.Names)!.GetDictionary(KnownName.EmbeddedFiles);
        await Assert.That(NameTreeInvariants.Check(root, out var count)).IsEmpty();
        await Assert.That(count).IsEqualTo(ManyFiles - (ManyFiles / Third));
        await EditChecks.AssertQpdfCleanAsync("attachments-many", EditChecks.SaveIncremental(document), cancellationToken);
    }

    /// <summary>A file attachment annotation holds a file, saves cleanly, and removing it deletes the file.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task FileAttachmentAnnotationRoundTrips(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var rectangle = new PdfRectangle(IconLeft, IconBottom, IconRight, IconTop);

        var id = PdfDocumentFileAttachmentAnnotations.AddFileAttachmentAnnotation(document, 1, rectangle, Sample(), PdfAttachmentIcon.Paperclip, "See the report");
        var compact = EditChecks.SaveCompact(document);
        using (var reopened = PdfDocumentReader.Open(compact, null))
        {
            var annotation = PdfDocumentFileAttachmentAnnotations.GetFileAttachmentAnnotations(reopened).Single();
            await Assert.That(annotation.PageIndex).IsEqualTo(1);
            await Assert.That(annotation.FileName).IsEqualTo(FileName);
            await Assert.That(annotation.Icon).IsEqualTo("Paperclip");
            await Assert.That(annotation.Contents).IsEqualTo("See the report");
            await Assert.That(PdfDocumentAssociatedFiles.GetAllAssociatedFiles(reopened)).IsEmpty();
        }

        await EditChecks.AssertQpdfCleanAsync("fileattachment-annotation", compact, cancellationToken);
        await Assert.That(PdfDocumentFileAttachmentAnnotations.RemoveFileAttachmentAnnotation(document, 1, id)).IsTrue();
        await Assert.That(PdfDocumentFileAttachmentAnnotations.GetFileAttachmentAnnotations(document)).IsEmpty();
        await Assert.That(Encoding.Latin1.GetString(EditChecks.SaveReadable(document))).DoesNotContain("/FileAttachment");
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentFileAttachmentAnnotations.GetFileAttachmentAnnotations(document).Count).IsEqualTo(1);
    }

    /// <summary>A certified document allows an annotation at level 3 but never a new attachment, and PDF/A-1 refuses both.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CertificationAndPdfALimitAttachments()
    {
        using var certified = PdfDocumentReader.Open(EditGuardSamples.Certified(Third), null);
        using var level1 = PdfDocumentReader.Open(EditGuardSamples.Certified(1), null);
        using var a1 = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A1B), null);
        var rectangle = new PdfRectangle(SmallNear, SmallNear, SmallFar, SmallFar);

        await Assert.That(() => PdfDocumentAttachmentEditing.AddAttachment(certified, Sample())).Throws<InvalidOperationException>();
        await Assert.That(() => PdfDocumentFileAttachmentAnnotations.AddFileAttachmentAnnotation(level1, 0, rectangle, Sample(), PdfAttachmentIcon.Tag, null)).Throws<InvalidOperationException>();
        await Assert.That(() => PdfDocumentAttachmentEditing.AddAttachment(a1, Sample())).Throws<InvalidOperationException>();
        await Assert.That(() => PdfDocumentFileAttachmentAnnotations.AddFileAttachmentAnnotation(a1, 0, rectangle, Sample(), PdfAttachmentIcon.Tag, null)).Throws<InvalidOperationException>();

        _ = PdfDocumentFileAttachmentAnnotations.AddFileAttachmentAnnotation(certified, 0, rectangle, Sample(), PdfAttachmentIcon.Graph, null);
        await Assert.That(PdfDocumentFileAttachmentAnnotations.GetFileAttachmentAnnotations(certified).Count).IsEqualTo(1);
    }

    /// <summary>A PDF/A-3 document gets the MIME type, relationship and associated files entry the level asks for.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfA3GetsAssociationFields()
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(new(PdfAPart3, "B")), null);

        _ = PdfDocumentAttachmentEditing.AddAttachment(document, new("data.xml", Content));

        var attachment = PdfDocumentAttachments.GetAttachments(document).Single();
        await Assert.That(attachment.MimeType).IsEqualTo("application/octet-stream");
        await Assert.That(attachment.Relationship).IsEqualTo(PdfFileRelationship.Unspecified);
        await Assert.That(PdfDocumentAssociatedFiles.GetAssociatedFiles(document).Count).IsEqualTo(1);
        await Assert.That(PdfDocumentAssociatedFiles.GetAssociatedFiles(document)[0].Relationship).IsEqualTo("Unspecified");
    }

    /// <summary>A name that is empty, used twice, or a MIME type that is not valid is refused before anything changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidSpecsAreRefused()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        _ = PdfDocumentAttachmentEditing.AddAttachment(document, Sample());

        await Assert.That(() => PdfDocumentAttachmentEditing.AddAttachment(document, Sample())).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentAttachmentEditing.AddAttachment(document, new(" ", Content))).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentAttachmentEditing.AddAttachment(document, new("b.bin", Content) { MimeType = "not a type" })).Throws<ArgumentException>();
        await Assert.That(PdfDocumentAttachments.GetAttachments(document).Count).IsEqualTo(1);
    }

    /// <summary>Works out the MD5 checksum of bytes with the library's own implementation.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The 16 byte hash.</returns>
    private static byte[] Hash(byte[] data)
    {
        var hash = new byte[Md5.HashLength];
        _ = Md5.HashData(data, hash);
        return hash;
    }

    /// <summary>Makes the sample file.</summary>
    /// <returns>The file.</returns>
    private static PdfAttachmentSpec Sample() => new(FileName, Content)
    {
        MimeType = Mime,
        Description = ReportText,
        Relationship = PdfFileRelationship.Data,
        Created = Date,
        Modified = Date.AddHours(1),
    };
}
