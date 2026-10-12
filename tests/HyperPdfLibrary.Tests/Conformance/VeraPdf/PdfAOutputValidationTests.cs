// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Creates PDF/A files with the library, saves them through every writer after a metadata edit, and checks each file with
/// veraPDF: it must keep its claim, pass the claimed profile, and read in HyperPdf as veraPDF reads it.
/// </summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class PdfAOutputValidationTests
{
    /// <summary>The PDF/A part whose /Info may hold only /ModDate.</summary>
    private const int Part4 = 4;

    /// <summary>Every file the library writes for a PDF/A level passes veraPDF and reads back the same.</summary>
    /// <param name="part">The PDF/A part.</param>
    /// <param name="conformance">The conformance level, or <see langword="null"/> for part 4.</param>
    /// <param name="cancellationToken">Cancels the saves and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, "B")]
    [Arguments(2, "B")]
    [Arguments(2, "U")]
    [Arguments(3, "B")]
    [Arguments(Part4, null)]
    public async Task LibraryOutputPassesVeraPdfAndReadsBack(int part, string? conformance, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        var level = new PdfAFixtureLevel(part, conformance);
        using var folder = new PdfAOutputFolder($"generated-{level.Flavour}");
        var source = Path.Combine(folder.Path, "source.pdf");
        await File.WriteAllBytesAsync(source, PdfAFixtures.Create(level), cancellationToken);
        var outputs = await PdfAOutputs.WriteAllAsync(source, folder.Path, cancellationToken);
        outputs["source"] = source;
        var results = await VeraPdfValidator.ValidateAsync(home, VeraPdfValidator.DetectFlavour, [.. outputs.Values], cancellationToken);
        foreach (var (name, path) in outputs)
        {
            var job = results[Path.GetFullPath(path)];
            await Assert.That(PdfAReadingComparison.ClaimFromProfile(job.ProfileName)).IsEqualTo(level.Label).Because($"{name} keeps its PDF/A-{level.Label} claim");
            await Assert.That(job.IsCompliant).IsTrue().Because($"{name} fails {job.FailureSummary}");
            await Assert.That(PdfAReadingComparison.Compare(path, job)).IsEmpty().Because($"{name} reads as veraPDF reads it");
            await AssertMetadataAsync(name, path, part);
        }
    }

    /// <summary>
    /// A custom XMP property passes veraPDF in PDF/A parts 1 to 3 only with its extension schema description: the
    /// optimiser's inferred-tags mark carries one, and the same property written without it fails.
    /// </summary>
    /// <param name="part">The PDF/A part.</param>
    /// <param name="cancellationToken">Cancels the saves and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task CustomXmpPropertyNeedsItsExtensionSchema(int part, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        var level = new PdfAFixtureLevel(part, "B");
        using var folder = new PdfAOutputFolder($"extension-{level.Flavour}");
        var described = Path.Combine(folder.Path, "described.pdf");
        var undescribed = Path.Combine(folder.Path, "undescribed.pdf");
        using (var document = PdfDocumentReader.Open(PdfAFixtures.Create(level), null))
        {
            InferredTagsXmp.Mark(document, new());
            await File.WriteAllBytesAsync(described, PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), cancellationToken);
        }

        using (var document = PdfDocumentReader.Open(PdfAFixtures.Create(level), null))
        {
            var change = new XmpChange(new(InferredTagsXmp.Prefix, InferredTagsXmp.Namespace, InferredTagsXmp.Property, XmpShape.Simple), InferredTagsXmp.Marked);
            var stream = document.Catalog.GetStream(KnownName.Metadata)!;
            var packet = XmpEditor.Apply(stream.DecodeToArray(cancellationToken), [change])!;
            StoreEditing.Replace(document.Objects, document.Catalog.GetRaw(KnownName.Metadata).AsReference(), PdfValue.FromStream(new(stream.Dictionary.Clone(), packet)));
            await File.WriteAllBytesAsync(undescribed, PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), cancellationToken);
        }

        var results = await VeraPdfValidator.ValidateAsync(home, level.Flavour, [described, undescribed], cancellationToken);
        var passing = results[Path.GetFullPath(described)];
        await Assert.That(passing.IsCompliant).IsTrue().Because($"the described property fails {passing.FailureSummary}");
        await Assert.That(results[Path.GetFullPath(undescribed)].IsCompliant).IsFalse().Because("an undescribed custom property breaks the claim");
        using var reopened = PdfDocumentReader.Open(described, null);
        await Assert.That(PdfDocumentMetadata.GetXmp(reopened)?.GetValue(InferredTagsXmp.Namespace, InferredTagsXmp.Property)).IsEqualTo(InferredTagsXmp.Marked);
    }

    /// <summary>PDF/A-4 needs no extension schema for a custom property, so the mark is written without one and still passes.</summary>
    /// <param name="cancellationToken">Cancels the save and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfA4CustomXmpPropertyNeedsNoExtensionSchema(CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("extension-4");
        var marked = Path.Combine(folder.Path, "marked.pdf");
        using (var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A4), null))
        {
            InferredTagsXmp.Mark(document, new());
            await File.WriteAllBytesAsync(marked, PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), cancellationToken);
        }

        var job = (await VeraPdfValidator.ValidateAsync(home, PdfAFixtureLevel.A4.Flavour, [marked], cancellationToken))[Path.GetFullPath(marked)];
        using var reopened = PdfDocumentReader.Open(marked, null);
        var xmp = PdfDocumentMetadata.GetXmp(reopened);
        await Assert.That(job.IsCompliant).IsTrue().Because($"the marked PDF/A-4 file fails {job.FailureSummary}");
        await Assert.That(xmp?.GetValue(InferredTagsXmp.Namespace, InferredTagsXmp.Property)).IsEqualTo(InferredTagsXmp.Marked);
        await Assert.That(Encoding.UTF8.GetString(xmp!.RawPacket)).DoesNotContain("pdfaExtension");
    }

    /// <summary>Checks that an output holds the edited title where its PDF/A part keeps it, and no conflict with its claim.</summary>
    /// <param name="name">The output's name.</param>
    /// <param name="path">The output.</param>
    /// <param name="part">The PDF/A part.</param>
    /// <returns>A task.</returns>
    private static async Task AssertMetadataAsync(string name, string path, int part)
    {
        using var document = PdfDocumentReader.Open(path, null);
        var report = PdfDocumentConformance.GetConformance(document);
        await Assert.That(report.HasConflicts).IsFalse().Because($"{name} has no feature its claim forbids");
        if (name is "source" or PdfAOutputs.Unedited)
        {
            await Assert.That(PdfDocumentMetadata.GetXmp(document)?.Title).IsEqualTo(PdfAFixtures.Title);
            return;
        }

        await Assert.That(PdfDocumentMetadata.GetXmp(document)?.Title).IsEqualTo(PdfAOutputs.EditedTitle).Because($"{name} keeps the edited XMP title");
        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsEqualTo(part == Part4 ? null : PdfAOutputs.EditedTitle).Because($"{name} follows the /Info rules of part {part}");
    }
}
