// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Generation;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks canonical fallback mappings and their leased working storage.</summary>
public sealed class BinaryUnicodeFallbackTests
{
    /// <summary>Canonical normalization keeps exactly one scalar or preserves the source.</summary>
    /// <param name="source">The inverse mapping's scalar.</param>
    /// <param name="expected">The resulting fallback scalar.</param>
    /// <param name="cancellationToken">Cancels normalization.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0x41, 0x41)]
    [Arguments(0x212B, 0xC5)]
    [Arguments(0x0344, 0x0344)]
    [Arguments(0xF900, 0x8C48)]
    [Arguments(0x2F800, 0x4E3D)]
    public async Task Fill_CanonicalScalar_PreservesSingleCharacter(int source, int expected, CancellationToken cancellationToken)
    {
        const int Count = 2;
        Span<int> values = stackalloc int[Count];
        values.Clear();
        BinaryUnicodeFallback.Fill(values, [new((uint)source, (uint)source, 1)], cancellationToken);
        var actual = values[1];
        await Assert.That(actual).IsEqualTo(expected);
    }

    /// <summary>Repeated fallback generation reuses its candidates and reads canonical scalar tables.</summary>
    /// <param name="count">The candidate count, exercising stack and pooled storage.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(2)]
    [Arguments(257)]
    public async Task Fill_WarmCanonicalMapping_DoesNotAllocate(int count, CancellationToken cancellationToken)
    {
        const uint Angstrom = 0x212B;
        const int Expected = 0xC5;
        const int MaxCount = 257;
        Span<int> values = stackalloc int[MaxCount];
        values = values[..count];
        values.Clear();
        BinaryUnicodeFallback.Fill(values, [new(Angstrom, Angstrom, 1)], cancellationToken);
        values.Clear();

        var before = GC.GetAllocatedBytesForCurrentThread();
        BinaryUnicodeFallback.Fill(values, [new(Angstrom, Angstrom, 1)], cancellationToken);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var actual = values[1];

        await Assert.That(actual).IsEqualTo(Expected);
        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>Explicit values bypass an inverse range without changing the table.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Fill_CompleteTable_ReturnsUnchanged(CancellationToken cancellationToken)
    {
        Span<int> values = [0x58, 0x59];
        var before = GC.GetAllocatedBytesForCurrentThread();
        BinaryUnicodeFallback.Fill(values, [new(0x41, 0x42, 0)], cancellationToken);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var first = values[0];
        var second = values[1];

        await Assert.That(first).IsEqualTo(0x58);
        await Assert.That(second).IsEqualTo(0x59);
        await Assert.That(allocated).IsEqualTo(0);
    }
}
