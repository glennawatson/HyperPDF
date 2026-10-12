// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Optimizing;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the adapter that writes PDF/A copies, and the saves that report a removed PDF/A claim.</summary>
public sealed class ArchiveCopyTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>Gets the levels a text document can be copied to, for <c>[MethodDataSource]</c>.</summary>
    /// <returns>The levels.</returns>
    public static IEnumerable<ArchiveLevel> UntaggedLevels() =>
    [
        ArchiveLevel.PdfA1B, ArchiveLevel.PdfA2B, ArchiveLevel.PdfA2U, ArchiveLevel.PdfA3B, ArchiveLevel.PdfA3U, ArchiveLevel.PdfA4,
    ];

    /// <summary>Every app level maps to its own library level and back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LevelsMapBothWays()
    {
        foreach (var level in Enum.GetValues<ArchiveLevel>())
        {
            var library = ArchiveMapping.ToLibrary(level);
            await Assert.That(ArchiveMapping.FromLibrary(library)).IsEqualTo(level);
            await Assert.That(library.ToString()).IsEqualTo(level.ToString());
        }
    }

    /// <summary>A copy is written at the chosen level, claims it and keeps the pages; the open file is untouched.</summary>
    /// <param name="level">The level.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(UntaggedLevels))]
    public async Task CopyMeetsTheLevel(ArchiveLevel level)
    {
        using var source = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var before = await File.ReadAllBytesAsync(source.FilePath);
        var writer = DocumentFeature<IArchiveCopyWriter>.Cast(source.Document)!;
        await using var output = new MemoryStream();

        var result = await writer.WriteArchiveCopyAsync(output, level, CancellationToken.None);

        await Assert.That(result.Converted).IsTrue().Because(string.Join("; ", result.Problems.Select(static p => p.Description)));
        await Assert.That(result.Level).IsEqualTo(level);
        using var copy = PdfDocumentReader.Open(output.ToArray(), null);
        await Assert.That(copy.PageCount).IsEqualTo(Pages);
        await Assert.That(PdfAValidator.ClaimedLevel(copy)).IsEqualTo(ArchiveMapping.ToLibrary(level));
        await Assert.That(PdfAValidator.Validate(copy)!.Passed).IsTrue();
        await Assert.That(await File.ReadAllBytesAsync(source.FilePath)).IsEquivalentTo(before);
    }

    /// <summary>A copy that cannot be made writes nothing and gives the reason.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusedCopyWritesNothing()
    {
        using var source = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var writer = DocumentFeature<IArchiveCopyWriter>.Cast(source.Document)!;
        await using var output = new MemoryStream();

        var result = await writer.WriteArchiveCopyAsync(output, ArchiveLevel.PdfA2A, CancellationToken.None);

        await Assert.That(result.Converted).IsFalse();
        await Assert.That(result.Problems).IsNotEmpty();
        await Assert.That(result.Problems.All(static p => p.Description.Length > 0)).IsTrue();
        await Assert.That(output.Length).IsEqualTo(0);
    }

    /// <summary>Saving a file that breaks its PDF/A claim removes the claim and reports it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaveReportsARemovedClaim()
    {
        using var source = new HyperPdfTestDocument(TestPdf.CreateWithFalsePdfAClaim());
        var editor = DocumentFeature<IAnnotationEditor>.Cast(source.Document)!;
        var report = new DocumentSaveReport();
        await using var output = new MemoryStream();

        var saved = await editor.SaveAsync(output, report, CancellationToken.None);

        await Assert.That(saved).IsTrue();
        await Assert.That(report.ClaimRemoved).IsNotNull();
        await Assert.That(report.ClaimRemoved!.Level).IsEqualTo(ArchiveLevel.PdfA2B);
        await Assert.That(report.ClaimRemoved.BrokenRules).IsNotEmpty();
        using var copy = PdfDocumentReader.Open(output.ToArray(), null);
        await Assert.That(PdfAValidator.ClaimedLevel(copy)).IsNull();
    }

    /// <summary>The optimiser's report carries a removed claim.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptimiseReportsARemovedClaim()
    {
        using var source = new HyperPdfTestDocument(TestPdf.CreateWithFalsePdfAClaim());
        var optimizer = DocumentFeature<IDocumentOptimizer>.Cast(source.Document)!;
        await using var output = new MemoryStream();

        var report = await optimizer.OptimizeAsync(output, new(OptimizePreset.KeepQuality, false, "en-AU", false, false), null, CancellationToken.None);

        await Assert.That(report.ClaimRemoved).IsNotNull();
        await Assert.That(report.ClaimRemoved!.Level).IsEqualTo(ArchiveLevel.PdfA2B);
    }
}
