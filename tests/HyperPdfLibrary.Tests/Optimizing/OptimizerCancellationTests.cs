// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Checks that the optimiser stops on a cancelled token during both its preparation and its writing.</summary>
[NotInParallel]
public sealed class OptimizerCancellationTests
{
    /// <summary>The pages of the heavy sample.</summary>
    private const int Pages = 6;

    /// <summary>The content bytes of each page.</summary>
    private const int Content = 8 * 1024 * 1024;

    /// <summary>The wait before the token is cancelled, in milliseconds.</summary>
    private const int CancelAfterMilliseconds = 40;

    /// <summary>The heavy sample.</summary>
    private static readonly byte[] Heavy = HeavyPdf.Create(Pages, Content);

    /// <summary>A token that is already cancelled stops both forms before any work.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledTokenStopsBothForms()
    {
        using var document = PdfDocumentReader.Open(Heavy, null);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await using var output = new MemoryStream();

        await Assert.That(() => PdfOptimizer.Optimize(document, output, PdfOptimizeOptions.Balanced, null, source.Token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await PdfOptimizer.OptimizeAsync(document, output, PdfOptimizeOptions.Balanced, null, source.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>A whole-document check stops on a cancelled token, even while it decodes a large stream.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CheckAsyncStopsOnCancel()
    {
        using var document = PdfDocumentReader.Open(Heavy, null);
        using var source = new CancellationTokenSource(CancelAfterMilliseconds);
        var cancelled = false;
        try
        {
            _ = await PdfDocumentCheck.CheckAsync(document, PdfCheckOptions.Default, source.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        await Assert.That(cancelled).IsTrue();
    }

    /// <summary>Cancelling a run in the middle stops it long before the uncancelled run would end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelInTheMiddleStopsEarly()
    {
        using var document = PdfDocumentReader.Open(Heavy, null);
        await using var output = new MemoryStream();
        var baseline = Stopwatch.GetTimestamp();
        _ = await PdfOptimizer.OptimizeAsync(document, output, PdfOptimizeOptions.Balanced, null, CancellationToken.None);
        var uncancelled = Stopwatch.GetElapsedTime(baseline);

        using var source = new CancellationTokenSource(CancelAfterMilliseconds);
        await using var second = new MemoryStream();
        var start = Stopwatch.GetTimestamp();
        var cancelled = false;
        try
        {
            _ = await PdfOptimizer.OptimizeAsync(document, second, PdfOptimizeOptions.Balanced, null, source.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        await Assert.That(cancelled).IsTrue();
        await Assert.That(Stopwatch.GetElapsedTime(start)).IsLessThan(uncancelled);
    }
}
