// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Tests.Conformance;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Checks that reusing optimiser state keeps reports and source documents independent.</summary>
[NotInParallel]
public sealed class OptimizerReuseTests
{
    /// <summary>The consecutive writes that exercise a returned validation stream.</summary>
    private const int ClaimedWrites = 2;

    /// <summary>The side length of the two image fixtures.</summary>
    private const int ImageSide = 96;

    /// <summary>The images that must be independently encoded.</summary>
    private const int EncodedImages = 2;

    /// <summary>The mean channel error allowed for JPEG quality 80.</summary>
    private const double LossyTolerance = 6;

    /// <summary>A later optimisation cannot overwrite the earlier report or retain its duplicate aliases.</summary>
    /// <param name="cancellationToken">Stops optimisation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedRunsKeepReportsAndOptionsIndependent(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(OptimizerSamples.Duplicates(), null);
        await using var firstOutput = new MemoryStream();
        var first = await PdfOptimizer.OptimizeAsync(document, firstOutput, PdfOptimizeOptions.KeepQuality, null, cancellationToken);
        var saving = first.GetSaving(PdfOptimizeCategory.Duplicates);
        await Assert.That(saving.Count).IsGreaterThan(0);

        await using var secondOutput = new MemoryStream();
        var second = await PdfOptimizer.OptimizeAsync(document, secondOutput, PdfOptimizeOptions.KeepQuality with { RemoveDuplicates = false }, null, cancellationToken);
        await Assert.That(second.GetSaving(PdfOptimizeCategory.Duplicates).Count).IsEqualTo(0);
        await Assert.That(first.GetSaving(PdfOptimizeCategory.Duplicates)).IsEqualTo(saving);

        await using var thirdOutput = new MemoryStream();
        var third = await PdfOptimizer.OptimizeAsync(document, thirdOutput, PdfOptimizeOptions.KeepQuality, null, cancellationToken);
        await Assert.That(third.GetSaving(PdfOptimizeCategory.Duplicates)).IsEqualTo(saving);
        using var reopened = PdfDocumentReader.Open(thirdOutput.ToArray(), null);
        await Assert.That(reopened.PageCount).IsEqualTo(document.PageCount);
    }

    /// <summary>Consecutive claimed rewrites validate complete output after the temporary stream has been returned.</summary>
    /// <param name="cancellationToken">Stops optimisation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedClaimedRunsKeepValidOutput(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A1B), null);
        long expectedLength = 0;
        for (var i = 0; i < ClaimedWrites; i++)
        {
            await using var output = new MemoryStream();
            var report = await PdfOptimizer.OptimizeAsync(document, output, PdfOptimizeOptions.KeepQuality, null, cancellationToken);
            using var reopened = PdfDocumentReader.Open(output.ToArray(), null);
            if (i == 0)
            {
                expectedLength = output.Length;
            }

            await Assert.That(report.BytesAfter).IsEqualTo(output.Length);
            await Assert.That(output.Length).IsEqualTo(expectedLength);
            var validation = await PdfAValidator.ValidateAsync(reopened, PdfALevel.PdfA1B, cancellationToken);
            await Assert.That(validation.Passed).IsTrue();
        }
    }

    /// <summary>Reusing encoded image storage preserves both images after their writer chunks are copied.</summary>
    /// <param name="cancellationToken">Stops optimisation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConsecutiveImagesKeepIndependentEncodedBytes(CancellationToken cancellationToken)
    {
        var first = OptimizerSamples.PhotoPixels(ImageSide, ImageSide);
        var second = new byte[first.Length];
        for (var i = 0; i < first.Length; i++)
        {
            second[i] = (byte)(byte.MaxValue - first[i]);
        }

        var source = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 192 96] /Resources << /XObject << /A 5 0 R /B 6 0 R >> >> /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, "q 96 0 0 96 0 0 cm /A Do Q q 96 0 0 96 96 0 cm /B Do Q\n"),
            OptimizerSamples.ImageObject(first, ImageSide, ImageSide, OptimizerSamples.Rgb),
            OptimizerSamples.ImageObject(second, ImageSide, ImageSide, OptimizerSamples.Rgb));
        using var document = PdfDocumentReader.Open(source, null);
        await using var output = new MemoryStream();
        var report = await PdfOptimizer.OptimizeAsync(document, output, PdfOptimizeOptions.Balanced with { RemoveDuplicates = false }, null, cancellationToken);
        await Assert.That(report.GetSaving(PdfOptimizeCategory.Images).Count).IsEqualTo(EncodedImages);
        await Assert.That(OptimizerTestKit.MeanDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(output.ToArray()))).IsLessThan(LossyTolerance);
    }
}
