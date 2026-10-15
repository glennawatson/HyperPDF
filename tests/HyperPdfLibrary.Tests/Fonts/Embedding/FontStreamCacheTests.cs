// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>The cache of compressed font programs returns the same bytes for the same program and stays bounded.</summary>
public sealed class FontStreamCacheTests
{
    /// <summary>The length of a test program.</summary>
    private const int ProgramLength = 4096;

    /// <summary>The length of a program too short to be kept.</summary>
    private const int ShortLength = 16;

    /// <summary>The entries the small test cache keeps.</summary>
    private const int SmallEntries = 3;

    /// <summary>The programs added beyond the entries a small cache keeps.</summary>
    private const int Overflow = 2;

    /// <summary>A byte budget that never limits a test.</summary>
    private const long Roomy = 1L << 30;

    /// <summary>A byte budget that holds two programs and their compressed forms but not three.</summary>
    private const long TwoPrograms = (ProgramLength * 2) + (ProgramLength / 2);

    /// <summary>A byte budget smaller than any program.</summary>
    private const long Tiny = 1;

    /// <summary>The seed of the program the shared-cache test uses.</summary>
    private const int SharedSeed = 7;

    /// <summary>The threads used by the concurrent test.</summary>
    private const int Threads = 8;

    /// <summary>The programs the concurrent test cycles through.</summary>
    private const int ConcurrentPrograms = 5;

    /// <summary>The lookups each concurrent thread makes.</summary>
    private const int LookupsPerThread = ConcurrentPrograms * 4;

    /// <summary>Spreads a position over the byte values.</summary>
    private const int Mix = 31;

    /// <summary>Moves a seed away from the neighbouring seeds.</summary>
    private const int SeedMix = 97;

    /// <summary>The bytes after which the base value of a program steps up.</summary>
    private const int Block = 64;

    /// <summary>The number of values the varying part of a byte takes.</summary>
    private const int Spread = 4;

    /// <summary>The shift that drops the low bits of the mixed value.</summary>
    private const int MixShift = 3;

    /// <summary>The cache under test.</summary>
    private FontStreamCacheState _state = FontStreamCacheState.Empty;

    /// <summary>The number of concurrent lookups that returned the wrong bytes.</summary>
    private int _failures;

    /// <summary>The same program returns the same compressed array.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SameProgramHits()
    {
        var first = FontStreamCache.GetOrCompress(ref _state, Program(1), SmallEntries, Roomy);
        var second = FontStreamCache.GetOrCompress(ref _state, Program(1), SmallEntries, Roomy);

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(_state.Entries.Length).IsEqualTo(1);
    }

    /// <summary>A program that differs in one byte misses and compresses to its own bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DifferentProgramMisses()
    {
        var one = Program(1);
        var other = Program(1);
        other[ProgramLength / Spread] ^= 1;

        var first = FontStreamCache.GetOrCompress(ref _state, one, SmallEntries, Roomy);
        var second = FontStreamCache.GetOrCompress(ref _state, other, SmallEntries, Roomy);

        await Assert.That(second).IsNotSameReferenceAs(first);
        await Assert.That(Inflate(first)).IsEquivalentTo(one);
        await Assert.That(Inflate(second)).IsEquivalentTo(other);
    }

    /// <summary>Programs shorter than the minimum are compressed on every call and not kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmallProgramIsNotKept()
    {
        var small = Program(1)[..ShortLength];

        var compressed = FontStreamCache.GetOrCompress(ref _state, small, SmallEntries, Roomy);

        await Assert.That(_state.Entries.Length).IsEqualTo(0);
        await Assert.That(Inflate(compressed)).IsEquivalentTo(small);
    }

    /// <summary>The oldest entry leaves when the entry limit is reached.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EntryCountIsBounded()
    {
        var first = FontStreamCache.GetOrCompress(ref _state, Program(1), SmallEntries, Roomy);
        for (var seed = 2; seed <= SmallEntries + Overflow; seed++)
        {
            _ = FontStreamCache.GetOrCompress(ref _state, Program(seed), SmallEntries, Roomy);
        }

        var again = FontStreamCache.GetOrCompress(ref _state, Program(1), SmallEntries, Roomy);

        await Assert.That(_state.Entries.Length).IsEqualTo(SmallEntries);
        await Assert.That(again).IsNotSameReferenceAs(first);
        await Assert.That(Inflate(again)).IsEquivalentTo(Program(1));
    }

    /// <summary>The kept bytes stay within the budget, and a program larger than the budget is returned but not kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ByteBudgetIsBounded()
    {
        for (var seed = 1; seed <= SmallEntries + Overflow; seed++)
        {
            _ = FontStreamCache.GetOrCompress(ref _state, Program(seed), int.MaxValue, TwoPrograms);
            await Assert.That(_state.Bytes).IsLessThanOrEqualTo(TwoPrograms);
        }

        var kept = _state.Entries.Length;
        var tooBig = FontStreamCache.GetOrCompress(ref _state, Program(SharedSeed), int.MaxValue, Tiny);

        await Assert.That(Inflate(tooBig)).IsEquivalentTo(Program(SharedSeed));
        await Assert.That(_state.Entries.Length).IsEqualTo(kept);
    }

    /// <summary>Threads sharing the cache each get the right bytes and the cache holds one entry per program.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentUseReturnsCorrectBytes(CancellationToken cancellationToken)
    {
        var tasks = new Task[Threads];
        for (var t = 0; t < tasks.Length; t++)
        {
            tasks[t] = Task.Factory.StartNew(
                static state => ((Worker)state!).Owner.Lookups(((Worker)state).Offset),
                new Worker(this, t),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        await Task.WhenAll(tasks);

        await Assert.That(Volatile.Read(ref _failures)).IsEqualTo(0);
        await Assert.That(_state.Entries.Length).IsEqualTo(ConcurrentPrograms);
    }

    /// <summary>The document cache gives the bytes plain compression gives.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentCacheMatchesPlainCompression()
    {
        var owner = new PdfFontEmbeddingState();
        var program = Program(SharedSeed);
        var plain = Encode(program);

        var cached = FontStreamCache.GetOrCompress(owner, program);

        await Assert.That(cached).IsEquivalentTo(plain);
        await Assert.That(FontStreamCache.GetOrCompress(owner, program)).IsSameReferenceAs(cached);
        await Assert.That(ReferenceEquals(FontStreamCache.GetOrCompress(new(), program), cached)).IsFalse();
    }

    /// <summary>Compresses a program without the cache.</summary>
    /// <param name="program">The program.</param>
    /// <returns>The zlib bytes.</returns>
    private static byte[] Encode(byte[] program)
    {
        var plain = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(program, ref plain);
            return plain.ToArray();
        }
        finally
        {
            plain.Dispose();
        }
    }

    /// <summary>Makes a compressible program from a seed.</summary>
    /// <param name="seed">The seed.</param>
    /// <returns>The program.</returns>
    private static byte[] Program(int seed)
    {
        var bytes = new byte[ProgramLength];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(((((i * Mix) + (seed * SeedMix)) >> MixShift) % Spread) + (i / Block));
        }

        return bytes;
    }

    /// <summary>Decompresses zlib bytes.</summary>
    /// <param name="compressed">The bytes.</param>
    /// <returns>The decompressed bytes.</returns>
    private static byte[] Inflate(byte[] compressed)
    {
        var output = default(PooledBuffer);
        try
        {
            FlateFilter.Decode(compressed, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Looks up programs from one thread, counting wrong results.</summary>
    /// <param name="offset">The program the thread starts at.</param>
    private void Lookups(int offset)
    {
        for (var i = 0; i < LookupsPerThread; i++)
        {
            var program = Program((i + offset) % ConcurrentPrograms);
            if (!Inflate(FontStreamCache.GetOrCompress(ref _state, program, int.MaxValue, Roomy)).AsSpan().SequenceEqual(program))
            {
                _ = Interlocked.Increment(ref _failures);
            }
        }
    }

    /// <summary>The state one concurrent thread starts with.</summary>
    /// <param name="Owner">The test.</param>
    /// <param name="Offset">The program the thread starts at.</param>
    private sealed record Worker(FontStreamCacheTests Owner, int Offset);
}
