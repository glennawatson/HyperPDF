// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Generation;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks compact resource decoding without network or generated files.</summary>
public sealed class BinaryCMapReaderTests
{
    /// <summary>A supplementary scalar requires only its independently owned output table.</summary>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Parse_SupplementaryMapping_AllocatesOnlyResult(CancellationToken cancellationToken)
    {
        const int TableLength = 2;
        ReadOnlySpan<byte> data = [0, 0x83, 1, 0, 1, 0xD8, 0x40, 0xDC, 0];
        _ = BinaryCMapReader.Parse(data, cancellationToken);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var expected = new int[TableLength];
        var resultBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        var map = BinaryCMapReader.Parse(data, cancellationToken);
        var actualBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(expected);

        await Assert.That(map.Unicode[1]).IsEqualTo(0x20000);
        await Assert.That(actualBytes).IsEqualTo(resultBytes);
    }

    /// <summary>Unused sixteen-byte Unicode strings consume no decoded-number storage.</summary>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Parse_WideUnicodeMapping_DoesNotAllocate(CancellationToken cancellationToken)
    {
        ReadOnlySpan<byte> data = [0, 0x8F, 1, 0, 1,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        _ = BinaryCMapReader.Parse(data, cancellationToken);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var map = BinaryCMapReader.Parse(data, cancellationToken);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(map.Unicode.Length).IsEqualTo(0);
        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>A signed CID decrease cannot wrap into a valid positive destination.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Parse_NegativeCidDestination_ThrowsDomainException() =>
        await Assert.That(static () => BinaryCMapReader.Parse([0, 0x41, 0x02, 0, 1, 0, 0, 0x05])).Throws<PdfException>();

    /// <summary>A CID-only resource does not reserve an unused Unicode table.</summary>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Parse_CidOnly_DoesNotAllocateUnicodeStorage(CancellationToken cancellationToken)
    {
        const long MaximumResultBytes = 1024;
        ReadOnlySpan<byte> data = [0, 0x41, 1, 0, 0x41, 1];
        _ = BinaryCMapReader.Parse(data, cancellationToken);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var map = BinaryCMapReader.Parse(data, cancellationToken);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(map.Ranges).IsEquivalentTo([new CidRange(0x41, 0x41, 1)]);
        await Assert.That(map.Unicode.Length).IsEqualTo(0);
        await Assert.That(allocated).IsLessThan(MaximumResultBytes);
    }

    /// <summary>An empty resource needs no mapping storage or result wrapper.</summary>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Parse_Empty_DoesNotAllocate(CancellationToken cancellationToken)
    {
        _ = BinaryCMapReader.Parse([0], cancellationToken);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var map = BinaryCMapReader.Parse([0], cancellationToken);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(map.Unicode.Length).IsEqualTo(0);
        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>Unused description metadata is validated without creating a string.</summary>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Parse_Description_DoesNotAllocate(CancellationToken cancellationToken)
    {
        _ = BinaryCMapReader.Parse([0, 0xE0, 1, 0x41], cancellationToken);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var map = BinaryCMapReader.Parse([0, 0xE0, 1, 0x41], cancellationToken);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(map.Parent).IsEqualTo(string.Empty);
        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>A later parse cannot overwrite a retained result's pooled-source data.</summary>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Parse_SequentialResults_RemainIndependent(CancellationToken cancellationToken)
    {
        const int TableLength = 2;
        var first = BinaryCMapReader.Parse([0, 0x81, 1, 0, 1, 0x30, 0x42], cancellationToken);
        var next = BinaryCMapReader.Parse([0, 0x81, 1, 0, 1, 0x30, 0x43], cancellationToken);

        await Assert.That(first.Unicode.Length).IsEqualTo(TableLength);
        await Assert.That(first.Unicode[0]).IsEqualTo(0);
        await Assert.That(first.Unicode[1]).IsEqualTo(0x3042);
        await Assert.That(next.Unicode[1]).IsEqualTo(0x3043);
    }

    /// <summary>Decodes two differential CID entries.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsDifferentialCidEntries()
    {
        var map = BinaryCMapReader.Parse([0, 0x41, 0x02, 0x82, 0xA0, 0x86, 0x4B, 0, 0]);
        await Assert.That(map.Ranges).IsEquivalentTo([new CidRange(0x82A0, 0x82A0, 0x034B), new CidRange(0x82A1, 0x82A1, 0x034C)]);
    }

    /// <summary>Decodes the UTF-16 value associated with a CID.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsUnicodeEntry()
    {
        var map = BinaryCMapReader.Parse([0, 0x81, 1, 0, 1, 0x30, 0x42]);
        await Assert.That(map.Unicode[1]).IsEqualTo(0x3042);
    }

    /// <summary>A UTF-16 surrogate pair remains one supplementary Unicode scalar.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsSupplementaryUnicodeEntry()
    {
        const int supplementary = 0x20000;
        var map = BinaryCMapReader.Parse([0, 0x83, 1, 0, 1, 0xD8, 0x40, 0xDC, 0]);
        await Assert.That(map.Unicode[1]).IsEqualTo(supplementary);
    }

    /// <summary>A Unicode range increments the encoded UTF-16 destination.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsSupplementaryUnicodeRange()
    {
        const int first = 0x20000;
        var map = BinaryCMapReader.Parse([0, 0xA3, 1, 0, 1, 1, 0xD8, 0x40, 0xDC, 0]);
        await Assert.That(map.Unicode[1]).IsEqualTo(first);
        await Assert.That(map.Unicode[2]).IsEqualTo(first + 1);
    }

    /// <summary>Truncated resources fail before any data can be published.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsTruncatedResource() =>
        await Assert.That(static () => BinaryCMapReader.Parse([0, 0x41, 1, 0x82])).Throws<PdfException>();

    /// <summary>Unknown block kinds are rejected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsUnknownBlock() =>
        await Assert.That(static () => BinaryCMapReader.Parse([0, 0xC0, 1])).Throws<PdfException>();

    /// <summary>Inverse Unicode mappings fill gaps without replacing explicit values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreservesExplicitUnicodeMappings()
    {
        int[] values = ['\0', 'X', '\0'];
        BinaryUnicodeFallback.Fill(values, [new(0x0041, 0x0042, 1)]);
        await Assert.That(values).IsEquivalentTo([0, 0x58, 0x42]);
    }

    /// <summary>Cancellation stops parsing and its scope does not affect the next parse.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledParseRestoresScope()
    {
        byte[] data = [0, 0x81, 1, 0, 1, 0x30, 0x42];
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.That(() => BinaryCMapReader.Parse(data, source.Token)).Throws<OperationCanceledException>();
        var map = BinaryCMapReader.Parse(data, CancellationToken.None);
        await Assert.That(map.Unicode[1]).IsEqualTo(0x3042);
    }

    /// <summary>Cancellation stops fallback before changing mappings.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledFallbackPreservesMappings()
    {
        int[] values = [0, 0];
        CidRange[] ranges = [new(0x41, 0x41, 1)];
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.That(() => BinaryUnicodeFallback.Fill(values, ranges, source.Token)).Throws<OperationCanceledException>();
        await Assert.That(values[1]).IsEqualTo(0);
        BinaryUnicodeFallback.Fill(values, ranges, CancellationToken.None);
        await Assert.That(values[1]).IsEqualTo(0x41);
    }

    /// <summary>Cancellation stops both table packers and the next pack succeeds.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledPackingRestoresScope()
    {
        int[] values = [0, 0x41];
        var map = BinaryCMapReader.Parse([0, 0x41, 1, 0x41, 1, 0], CancellationToken.None);
        var maps = new Dictionary<string, BinaryCMapData>(StringComparer.Ordinal) { ["H"] = map };
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.That(() => PackedTables.PackUnicode(values, source.Token)).Throws<OperationCanceledException>();
        await Assert.That(() => PackedTables.PackCMap(maps, "H", Collections.All[0], source.Token)).Throws<OperationCanceledException>();
        await Assert.That(PackedTables.PackUnicode(values, CancellationToken.None).Length).IsGreaterThan(0);
        await Assert.That(PackedTables.PackCMap(maps, "H", Collections.All[0], CancellationToken.None).Length).IsGreaterThan(0);
    }
}
