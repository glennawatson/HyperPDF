// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Checks decoder cleanup after an interrupted zlib or raw deflate read.</summary>
public sealed class ZLibCodecTests
{
    /// <summary>The source length, large enough for decoding to enter its output loop.</summary>
    private const int InputLength = 8192;

    /// <summary>The bytes in the zlib header.</summary>
    private const int HeaderLength = 2;

    /// <summary>The bytes in the zlib Adler-32 checksum.</summary>
    private const int ChecksumLength = 4;

    /// <summary>An interrupted decode leaves the next decode able to read a whole stream.</summary>
    /// <param name="raw">Whether the input uses raw deflate.</param>
    /// <param name="testToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InterruptedDecodeDoesNotAffectNextDecode(bool raw, CancellationToken testToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(testToken);
        await cancellation.CancelAsync();
        var result = DecodeAfterCancel(raw, cancellation.Token, testToken);
        await Assert.That(result.Cancelled).IsTrue();
        await Assert.That(result.Complete).IsTrue();
        await Assert.That(result.Matches).IsTrue();
    }

    /// <summary>Runs two decodes without retaining buffers across an await.</summary>
    /// <param name="raw">Whether the input uses raw deflate.</param>
    /// <param name="cancelledToken">The cancelled token for the first decode.</param>
    /// <param name="testToken">Cancels the second decode.</param>
    /// <returns>The results of both decodes.</returns>
    private static DecodeResult DecodeAfterCancel(bool raw, CancellationToken cancelledToken, CancellationToken testToken)
    {
        var original = new byte[InputLength];
        for (var i = 0; i < original.Length; i++)
        {
            original[i] = (byte)i;
        }

        var encoded = default(PooledBuffer);
        try
        {
            ZLibCodec.Compress(original, ref encoded);
            var input = raw ? encoded.WrittenSpan[HeaderLength..^ChecksumLength].ToArray() : encoded.ToArray();
            var first = default(PooledBuffer);
            var cancelled = false;
            try
            {
                _ = ZLibCodec.Decompress(input, raw, ref first, cancelledToken);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            finally
            {
                first.Dispose();
            }

            var second = default(PooledBuffer);
            try
            {
                var complete = ZLibCodec.Decompress(input, raw, ref second, testToken);
                return new(cancelled, complete, second.WrittenSpan.SequenceEqual(original));
            }
            finally
            {
                second.Dispose();
            }
        }
        finally
        {
            encoded.Dispose();
        }
    }

    /// <summary>The outcome of interrupted and subsequent decompressions.</summary>
    /// <param name="Cancelled">Whether the first decode was cancelled.</param>
    /// <param name="Complete">Whether the next decode completed.</param>
    /// <param name="Matches">Whether the next decoded data matched the source.</param>
    private readonly record struct DecodeResult(bool Cancelled, bool Complete, bool Matches);
}
