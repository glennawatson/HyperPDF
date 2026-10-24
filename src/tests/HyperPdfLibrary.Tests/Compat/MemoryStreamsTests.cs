// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Compat;

namespace HyperPdfLibrary.Tests.Compat;

/// <summary>Checks memory-stream segments on both supported runtimes.</summary>
public sealed class MemoryStreamsTests
{
    /// <summary>The segment starts after this many prefix bytes.</summary>
    private const int SegmentStart = 3;

    /// <summary>The expected segment byte count.</summary>
    private const int SegmentLength = 4;

    /// <summary>A sliced memory stream excludes the prefix and suffix and keeps segment-relative positions.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task SegmentHasOnlyTheRequestedBytes()
    {
        var bytes = "preDATAsuffix"u8.ToArray();
        await using var stream = MemoryStreams.OpenRead(bytes.AsMemory(SegmentStart, SegmentLength));
        var output = new byte[SegmentLength];
        await Assert.That(stream.Length).IsEqualTo(SegmentLength);
        await Assert.That(stream.Position).IsEqualTo(0);
        await Assert.That(await stream.ReadAsync(output.AsMemory(), CancellationToken.None)).IsEqualTo(SegmentLength);
        await Assert.That(output.AsSpan().SequenceEqual("DATA"u8)).IsTrue();
        await Assert.That(stream.ReadByte()).IsEqualTo(-1);
        await Assert.That(stream.Seek(0, SeekOrigin.Begin)).IsEqualTo(0);
        await Assert.That(stream.ReadByte()).IsEqualTo((int)'D');
        await Assert.That(stream.Position).IsEqualTo(1);
    }

    /// <summary>Empty memory opens as an empty readable stream.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task EmptyMemoryIsReadable()
    {
        await using var stream = MemoryStreams.OpenRead(ReadOnlyMemory<byte>.Empty);
        await Assert.That(stream.Length).IsEqualTo(0);
        await Assert.That(stream.ReadByte()).IsEqualTo(-1);
        await Assert.That(stream.CanRead).IsTrue();
    }

    /// <summary>The stream cannot change its input through write operations.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task WritesAreRejected()
    {
        await using var stream = MemoryStreams.OpenRead("Data"u8.ToArray());
        await Assert.That(stream.CanWrite).IsFalse();
        await Assert.That(() => stream.WriteByte(0)).Throws<NotSupportedException>();
    }
}
