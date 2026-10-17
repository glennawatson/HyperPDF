// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Checks early rejection and detached winning JBIG2 bytes.</summary>
public sealed class BilevelJbig2Tests
{
    /// <summary>The synthetic page width.</summary>
    private const int Width = 64;

    /// <summary>The synthetic page height.</summary>
    private const int Height = 2;

    /// <summary>The bits in a packed byte.</summary>
    private const int ByteBits = 8;

    /// <summary>A coding cannot beat a non-positive byte limit.</summary>
    /// <param name="limit">The byte limit.</param>
    /// <param name="cancellationToken">Cancels the selection.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0L)]
    [Arguments(-1L)]
    public async Task NonPositiveLimitSkipsEncoding(long limit, CancellationToken cancellationToken)
    {
        var pools = new ScratchPools();
        try
        {
            _ = MeasureRejected(pools, limit, cancellationToken);
            var (rejected, allocated) = MeasureRejected(pools, limit, cancellationToken);
            await Assert.That(rejected).IsTrue();
            await Assert.That(allocated).IsEqualTo(0);
            await Assert.That(pools.RetainedBytes).IsEqualTo(0);
        }
        finally
        {
            pools.Close();
        }
    }

    /// <summary>An early byte-limit rejection cannot bypass cancellation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CancelledZeroLimitStillThrows()
    {
        var pools = new ScratchPools();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        try
        {
            await Assert.That(() => BilevelJbig2.Choose([], Width, Height, PdfJbig2Mode.Lossless, 0, pools, source.Token))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            pools.Close();
        }
    }

    /// <summary>A returned coding owns its bytes independently of the next operation.</summary>
    /// <param name="cancellationToken">Cancels encoding.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WinningBytesRemainDetached(CancellationToken cancellationToken)
    {
        var pools = new ScratchPools();
        var rows = new byte[Width * Height / ByteBits];
        try
        {
            var first = BilevelJbig2.Choose(rows, Width, Height, PdfJbig2Mode.Lossless, long.MaxValue, pools, cancellationToken)!;
            var expected = first.Data.ToArray();
            rows.AsSpan().Fill(byte.MaxValue);
            var second = BilevelJbig2.Choose(rows, Width, Height, PdfJbig2Mode.Lossless, long.MaxValue, pools, cancellationToken)!;

            await Assert.That(first.Data).IsEquivalentTo(expected);
            await Assert.That(second.Data).IsNotSameReferenceAs(first.Data);
            await Assert.That(first.IsLossless).IsTrue();
            await Assert.That(second.IsLossless).IsTrue();
        }
        finally
        {
            pools.Close();
        }
    }

    /// <summary>Measures the limit-only early exit without creating sample bytes.</summary>
    /// <param name="pools">The caller's encoding owner.</param>
    /// <param name="limit">The non-positive byte limit.</param>
    /// <param name="cancellationToken">Cancels the selection.</param>
    /// <returns>The rejection flag and allocated bytes.</returns>
    private static RejectedCoding MeasureRejected(ScratchPools pools, long limit, CancellationToken cancellationToken)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = BilevelJbig2.Choose([], Width, Height, PdfJbig2Mode.Lossless, limit, pools, cancellationToken);
        return new(result is null, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>A budget-only rejection and its allocation count.</summary>
    /// <param name="Rejected">Whether no encoding was produced.</param>
    /// <param name="Allocated">The managed allocation count.</param>
    private readonly record struct RejectedCoding(bool Rejected, long Allocated);
}
