// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2.Encoding;

/// <summary>Checks caller-owned MMR line buffers and cancellation boundaries.</summary>
public sealed class Jbig2ScratchOwnershipTests
{
    /// <summary>The width that requires owner-retained line buffers.</summary>
    private const int Width = 16_384;

    /// <summary>The rows in the synthetic page.</summary>
    private const int Height = 2;

    /// <summary>The bits per packed byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The warm encode repetitions.</summary>
    private const int Repetitions = 16;

    /// <summary>The fixed MMR settings.</summary>
    private static readonly Jbig2EncodeOptions Options = new() { Mode = Jbig2EncodeMode.GenericRegionMmr };

    /// <summary>The fixed arithmetic settings, which need no MMR owner.</summary>
    private static readonly Jbig2EncodeOptions ArithmeticOptions = new() { Mode = Jbig2EncodeMode.GenericRegion };

    /// <summary>A closed owner cannot retain buffers returned by a later encode.</summary>
    /// <param name="cancellationToken">Cancels encoding.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClosedOwnerDoesNotRetainMmrBuffers(CancellationToken cancellationToken)
    {
        var pools = new ScratchPools();
        pools.Close();
        var result = Encode(pools, Rows(), cancellationToken);

        await Assert.That(result.Mode).IsEqualTo(Jbig2EncodeMode.GenericRegionMmr);
        await Assert.That(result.Length).IsGreaterThan(0);
        await Assert.That(pools.RetainedBytes).IsEqualTo(0);
    }

    /// <summary>Separate callers keep their own large line-buffer storage.</summary>
    /// <param name="cancellationToken">Cancels encoding.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SeparateOwnersDoNotShareRetainedBuffers(CancellationToken cancellationToken)
    {
        var first = new ScratchPools();
        var second = new ScratchPools();
        try
        {
            var rows = Rows();
            _ = Encode(first, rows, cancellationToken);
            var before = first.RetainedBytes;
            _ = Encode(second, rows, cancellationToken);

            await Assert.That(before).IsGreaterThan(0);
            await Assert.That(first.RetainedBytes).IsEqualTo(before);
            await Assert.That(second.RetainedBytes).IsEqualTo(before);
            first.Close();
            await Assert.That(second.RetainedBytes).IsEqualTo(before);
        }
        finally
        {
            first.Close();
            second.Close();
        }
    }

    /// <summary>Warm MMR encoding uses the caller's buffers without managed allocations.</summary>
    /// <param name="cancellationToken">Cancels encoding.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WarmMmrEncodingDoesNotAllocate(CancellationToken cancellationToken)
    {
        var pools = new ScratchPools();
        try
        {
            var allocated = MeasureRepeated(pools, Rows(), cancellationToken);
            await Assert.That(allocated).IsEqualTo(0);
        }
        finally
        {
            pools.Close();
        }
    }

    /// <summary>Arithmetic encoding does not construct unused MMR ownership state.</summary>
    /// <param name="cancellationToken">Cancels encoding.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WarmPublicArithmeticDoesNotAllocate(CancellationToken cancellationToken)
    {
        var allocated = MeasurePublicArithmetic(Rows(), cancellationToken);
        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>Cancellation leaves the output unchanged and does not rent line buffers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CancelledMmrDoesNotWriteOrRent()
    {
        var pools = new ScratchPools();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        try
        {
            var cancelled = WriteCancelled(pools, source.Token);
            await Assert.That(cancelled).IsTrue();
            await Assert.That(pools.RetainedBytes).IsEqualTo(0);
        }
        finally
        {
            pools.Close();
        }
    }

    /// <summary>Attempts an MMR region write using an already cancelled token.</summary>
    /// <param name="pools">The line-buffer owner.</param>
    /// <param name="cancellationToken">The cancelled token.</param>
    /// <returns>True when cancellation happened before any output was written.</returns>
    private static bool WriteCancelled(ScratchPools pools, CancellationToken cancellationToken)
    {
        var output = default(PooledBuffer);
        try
        {
            Jbig2GenericRegionWriter.WriteMmr(ref output, 0, new(Rows(), Width, Height), default, pools, cancellationToken);
            return false;
        }
        catch (OperationCanceledException)
        {
            return output.Length == 0;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Measures repeated synchronous encodes after warming all buffer pools.</summary>
    /// <param name="pools">The line-buffer owner.</param>
    /// <param name="rows">The synthetic page.</param>
    /// <param name="cancellationToken">Cancels encoding.</param>
    /// <returns>The managed bytes allocated in the repeated loop.</returns>
    private static long MeasureRepeated(ScratchPools pools, byte[] rows, CancellationToken cancellationToken)
    {
        var data = default(PooledBuffer);
        var globals = default(PooledBuffer);
        try
        {
            _ = Jbig2Encoder.EncodeInto(rows, new(new(Width, Height), Options), ref data, ref globals, pools, cancellationToken);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < Repetitions; i++)
            {
                _ = Jbig2Encoder.EncodeInto(rows, new(new(Width, Height), Options), ref data, ref globals, pools, cancellationToken);
            }

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        finally
        {
            data.Dispose();
            globals.Dispose();
        }
    }

    /// <summary>Measures the public pooled arithmetic path after warming its buffers.</summary>
    /// <param name="rows">The synthetic page.</param>
    /// <param name="cancellationToken">Cancels encoding.</param>
    /// <returns>The managed bytes allocated by repeated encodes.</returns>
    private static long MeasurePublicArithmetic(byte[] rows, CancellationToken cancellationToken)
    {
        var data = default(PooledBuffer);
        var globals = default(PooledBuffer);
        try
        {
            _ = Jbig2Encoder.Encode(rows, Width, Height, ArithmeticOptions, ref data, ref globals, cancellationToken);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < Repetitions; i++)
            {
                _ = Jbig2Encoder.Encode(rows, Width, Height, ArithmeticOptions, ref data, ref globals, cancellationToken);
            }

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        finally
        {
            data.Dispose();
            globals.Dispose();
        }
    }

    /// <summary>Encodes without allocating a detached output snapshot.</summary>
    /// <param name="pools">The line-buffer owner.</param>
    /// <param name="rows">The synthetic page.</param>
    /// <param name="cancellationToken">Cancels encoding.</param>
    /// <returns>The chosen mode and output length.</returns>
    private static EncodedLength Encode(ScratchPools pools, byte[] rows, CancellationToken cancellationToken)
    {
        var data = default(PooledBuffer);
        var globals = default(PooledBuffer);
        try
        {
            var mode = Jbig2Encoder.EncodeInto(rows, new(new(Width, Height), Options), ref data, ref globals, pools, cancellationToken);
            return new(mode, data.Length);
        }
        finally
        {
            data.Dispose();
            globals.Dispose();
        }
    }

    /// <summary>Creates white packed sample rows in memory.</summary>
    /// <returns>The synthetic sample bytes.</returns>
    private static byte[] Rows()
    {
        var rows = new byte[Width * Height / ByteBits];
        rows.AsSpan().Fill(byte.MaxValue);
        return rows;
    }

    /// <summary>The coding mode and byte length, without a retained output array.</summary>
    /// <param name="Mode">The selected mode.</param>
    /// <param name="Length">The coded byte length.</param>
    private readonly record struct EncodedLength(Jbig2EncodeMode Mode, int Length);
}
