// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET11_0_OR_GREATER
using System.IO.Compression;
#endif
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Tests.Compat;

/// <summary>Checks explicit decoder ownership and early exits.</summary>
public sealed class ZLibCodecTests
{
#if NET11_0_OR_GREATER
    /// <summary>The decodes performed with warm native state.</summary>
    private const int RepeatedDecodes = 128;

    /// <summary>The bytes occupied by the zlib header.</summary>
    private const int HeaderLength = 2;
#endif

    /// <summary>Gets the decoded sample bytes.</summary>
    private static ReadOnlySpan<byte> Source => "Owned decoder state"u8;

    /// <summary>A closed owner cannot retain state returned by a later operation.</summary>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Decompress_ClosedOwner_ReturnsCorrectBytes(CancellationToken cancellationToken)
    {
        var pools = new ScratchPools();
        pools.Close();
        var bytes = Compress();

        await Assert.That(Decode(pools, bytes, 0, cancellationToken)).IsEquivalentTo(Source.ToArray());
#if NET11_0_OR_GREATER
        await Assert.That(pools.Objects<ZLibDecoder>().Take()).IsNull();
#endif
    }

    /// <summary>An empty input does not construct or rent decoder state.</summary>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Decompress_EmptyInput_DoesNotAllocate(CancellationToken cancellationToken)
    {
        var pools = new ScratchPools();
        try
        {
            _ = MeasureEmpty(pools, cancellationToken);
            var (complete, length, allocated) = MeasureEmpty(pools, cancellationToken);

            await Assert.That(complete).IsFalse();
            await Assert.That(length).IsEqualTo(0);
            await Assert.That(allocated).IsEqualTo(0);
        }
        finally
        {
            pools.Close();
        }
    }

#if NET11_0_OR_GREATER
    /// <summary>Each explicit owner retains only its own decoder between operations.</summary>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Decompress_SeparateOwners_DoesNotShareDecoders(CancellationToken cancellationToken)
    {
        var first = new ScratchPools();
        var second = new ScratchPools();
        try
        {
            var bytes = Compress();
            _ = Decode(first, bytes, 0, cancellationToken);
            using var decoder = first.Objects<ZLibDecoder>().Take();
            _ = Decode(second, bytes, 0, cancellationToken);
            using var other = second.Objects<ZLibDecoder>().Take();

            await Assert.That(decoder).IsNotNull();
            await Assert.That(other).IsNotNull();
            await Assert.That(other).IsNotSameReferenceAs(decoder);
        }
        finally
        {
            first.Close();
            second.Close();
        }
    }

    /// <summary>Raw and zlib decoding reuse native state without managed allocations once warm.</summary>
    /// <param name="raw">Whether to omit the zlib header.</param>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Decompress_WarmOwner_DoesNotAllocate(bool raw, CancellationToken cancellationToken)
    {
        var pools = new ScratchPools();
        try
        {
            var bytes = Compress();
            var allocated = MeasureRepeated(pools, bytes, raw, cancellationToken);

            await Assert.That(allocated).IsEqualTo(0);
        }
        finally
        {
            pools.Close();
        }
    }

    /// <summary>A rejected expansion returns its decoder and a subsequent decode resets it.</summary>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Decompress_OutputLimit_ReturnsUsableDecoder(CancellationToken cancellationToken)
    {
        var pools = new ScratchPools();
        try
        {
            var bytes = Compress();
            _ = Decode(pools, bytes, 0, cancellationToken);
            var idle = pools.Objects<ZLibDecoder>();
            var decoder = idle.Take()!;
            _ = idle.Offer(decoder);

            await Assert.That(() => Decode(pools, bytes, 1, cancellationToken)).Throws<PdfException>();
            var returned = idle.Take();
            await Assert.That(returned).IsSameReferenceAs(decoder);
            _ = idle.Offer(returned!);
            await Assert.That(Decode(pools, bytes, 0, cancellationToken)).IsEquivalentTo(Source.ToArray());
        }
        finally
        {
            pools.Close();
        }
    }

    /// <summary>Cancellation occurs before decoder rental, even when the input is empty.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Decompress_CancelledEmptyInput_DoesNotRentDecoder()
    {
        var pools = new ScratchPools();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        try
        {
            var idle = pools.Objects<ZLibDecoder>();
            var decoder = new ZLibDecoder();
            _ = idle.Offer(decoder);

            await Assert.That(() => Decode(pools, [], 0, cancellation.Token)).Throws<OperationCanceledException>();
            using var returned = idle.Take();
            await Assert.That(returned).IsSameReferenceAs(decoder);
        }
        finally
        {
            pools.Close();
        }
    }

    /// <summary>Measures synchronous native decoding with one reused output buffer.</summary>
    /// <param name="pools">The explicit decoder owner.</param>
    /// <param name="bytes">The zlib payload.</param>
    /// <param name="raw">Whether to start after the zlib header.</param>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>The bytes allocated by repeated decodes.</returns>
    private static long MeasureRepeated(ScratchPools pools, byte[] bytes, bool raw, CancellationToken cancellationToken)
    {
        var output = default(PooledBuffer);
        var input = raw ? bytes.AsSpan(HeaderLength) : bytes.AsSpan();
        try
        {
            _ = ZLibCodec.Decompress(input, raw, ref output, pools, cancellationToken);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < RepeatedDecodes; i++)
            {
                output.Length = 0;
                _ = ZLibCodec.Decompress(input, raw, ref output, pools, cancellationToken);
            }

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        finally
        {
            output.Dispose();
        }
    }
#else
    /// <summary>A limit failure detaches pinned input and returns the wrapper to its owner.</summary>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Decompress_OutputLimit_ReturnsDetachedSource(CancellationToken cancellationToken)
    {
        var pools = new ScratchPools();
        try
        {
            var bytes = Compress();
            _ = Decode(pools, bytes, 0, cancellationToken);
            var idle = pools.Objects<EndTrackingStream>();
            var source = idle.Take()!;
            _ = idle.Offer(source);

            await Assert.That(() => Decode(pools, bytes, 1, cancellationToken)).Throws<PdfException>();
            var returned = idle.Take();
            await Assert.That(returned).IsSameReferenceAs(source);
            await Assert.That(returned!.CanRead).IsFalse();
            await Assert.That(returned.Length).IsEqualTo(0);
            _ = idle.Offer(returned);
            await Assert.That(Decode(pools, bytes, 0, cancellationToken)).IsEquivalentTo(Source.ToArray());
        }
        finally
        {
            pools.Close();
        }
    }

    /// <summary>Truncation state resets before the wrapper reads another stream.</summary>
    /// <param name="raw">Whether to decode raw deflate.</param>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Decompress_TruncatedThenComplete_ResetsSource(bool raw, CancellationToken cancellationToken)
    {
        const int HeaderLength = 2;
        var pools = new ScratchPools();
        try
        {
            var bytes = Compress();
            var start = raw ? HeaderLength : 0;
            bool truncated;
            bool complete;
            byte[] decoded;
            {
                var output = default(PooledBuffer);
                try
                {
                    truncated = ZLibCodec.Decompress(bytes.AsSpan(start, 1), raw, ref output, pools, cancellationToken);
                    output.Length = 0;
                    complete = ZLibCodec.Decompress(bytes.AsSpan(start), raw, ref output, pools, cancellationToken);
                    decoded = output.ToArray();
                }
                finally
                {
                    output.Dispose();
                }
            }

            await Assert.That(truncated).IsFalse();
            await Assert.That(complete).IsTrue();
            await Assert.That(decoded).IsEquivalentTo(Source.ToArray());
        }
        finally
        {
            pools.Close();
        }
    }
#endif

    /// <summary>Measures the empty-input early return without constructing an output array.</summary>
    /// <param name="pools">The explicit decoder owner.</param>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>The empty decoder result and allocated byte count.</returns>
    private static EmptyDecodeResult MeasureEmpty(ScratchPools pools, CancellationToken cancellationToken)
    {
        var output = default(PooledBuffer);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var complete = ZLibCodec.Decompress([], false, ref output, pools, cancellationToken);
        return new(complete, output.Length, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>Decodes into an independent result using an optional logical output limit.</summary>
    /// <param name="pools">The explicit decoder owner.</param>
    /// <param name="bytes">The zlib payload.</param>
    /// <param name="limit">The logical limit, or zero for the general limit.</param>
    /// <param name="cancellationToken">Cancels decompression.</param>
    /// <returns>The detached decoded bytes.</returns>
    private static byte[] Decode(ScratchPools pools, byte[] bytes, int limit, CancellationToken cancellationToken)
    {
        var output = default(PooledBuffer);
        if (limit > 0)
        {
            output.MaximumLength = limit;
        }

        try
        {
            _ = ZLibCodec.Decompress(bytes, false, ref output, pools, cancellationToken);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Creates a small compressed payload in memory.</summary>
    /// <returns>The zlib bytes.</returns>
    private static byte[] Compress()
    {
        var output = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(Source, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>The result of an empty compressed-input operation.</summary>
    /// <param name="Complete">Whether a complete compressed stream was present.</param>
    /// <param name="Length">The decoded byte count.</param>
    /// <param name="Allocated">The managed bytes allocated.</param>
    private readonly record struct EmptyDecodeResult(bool Complete, int Length, long Allocated);
}
