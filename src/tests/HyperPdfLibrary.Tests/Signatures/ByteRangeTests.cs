// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>Tests for checking /ByteRange arrays against a file.</summary>
public sealed class ByteRangeTests
{
    /// <summary>The length of the signed prefix in the sample.</summary>
    private const int Prefix = 4;

    /// <summary>The position after the /Contents string in the sample.</summary>
    private const int AfterContents = 10;

    /// <summary>The length of the signed suffix in the sample.</summary>
    private const int Suffix = 4;

    /// <summary>The decoded length of the sample's /Contents string.</summary>
    private const int ContentsLength = 2;

    /// <summary>A number past the end of the sample.</summary>
    private const int TooFar = 100;

    /// <summary>Gets a sample: four signed bytes, a two-byte hex string, four signed bytes.</summary>
    private static byte[] File { get; } = "abcd<0A0B>efgh"u8.ToArray();

    /// <summary>Gets the sample's one revision.</summary>
    private static PdfRevision[] Revisions { get; } = [new(0, 0, File.Length, File.Length)];

    /// <summary>A range that skips exactly the /Contents string and reaches the end is valid and covers the file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidRangeCoversFile()
    {
        var check = Check([0, Prefix, AfterContents, Suffix], ContentsLength);

        await Assert.That(check.Status).IsEqualTo(PdfByteRangeStatus.Valid);
        await Assert.That(check.CoversWholeDocument).IsTrue();
        await Assert.That(check.SignedLength).IsEqualTo(Prefix + Suffix);
        await Assert.That(check.RevisionIndex).IsEqualTo(0);
    }

    /// <summary>An odd number of entries is malformed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OddCountIsMalformed() =>
        await Assert.That(Check([0, Prefix, AfterContents], ContentsLength).Status).IsEqualTo(PdfByteRangeStatus.Malformed);

    /// <summary>A negative entry is malformed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NegativeIsMalformed() =>
        await Assert.That(Check([0, -1, AfterContents, Suffix], ContentsLength).Status).IsEqualTo(PdfByteRangeStatus.Malformed);

    /// <summary>A range past the end of the file is out of bounds.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PastEndIsOutOfBounds() =>
        await Assert.That(Check([0, Prefix, AfterContents, TooFar], ContentsLength).Status).IsEqualTo(PdfByteRangeStatus.OutOfBounds);

    /// <summary>A range that starts inside the previous one overlaps it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlapIsReported() =>
        await Assert.That(Check([0, AfterContents, Prefix, Suffix], ContentsLength).Status).IsEqualTo(PdfByteRangeStatus.Overlapping);

    /// <summary>A range that starts before the previous one is out of order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BackwardsIsOutOfOrder() =>
        await Assert.That(Check([AfterContents, Suffix, 0, Prefix], ContentsLength).Status).IsEqualTo(PdfByteRangeStatus.OutOfOrder);

    /// <summary>A gap that leaves out more than the /Contents string is rejected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WiderGapIsRejected() =>
        await Assert.That(Check([0, Prefix - 1, AfterContents, Suffix], ContentsLength).Status).IsEqualTo(PdfByteRangeStatus.GapNotContents);

    /// <summary>A gap whose length does not match the /Contents value is rejected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MismatchedContentsIsRejected() =>
        await Assert.That(Check([0, Prefix, AfterContents, Suffix], ContentsLength + 1).Status).IsEqualTo(PdfByteRangeStatus.GapNotContents);

    /// <summary>An oversized /Contents is rejected before anything is read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OversizeContentsIsTooLarge() =>
        await Assert.That(Check([0, Prefix, AfterContents, Suffix], PdfByteRangeChecker.MaxContentsLength + 1).Status).IsEqualTo(PdfByteRangeStatus.TooLarge);

    /// <summary>A range that stops short of the end is valid but does not cover the file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShortRangeDoesNotCover()
    {
        var check = Check([0, Prefix, AfterContents, Suffix - 1], ContentsLength);

        await Assert.That(check.Status).IsEqualTo(PdfByteRangeStatus.Valid);
        await Assert.That(check.CoversWholeDocument).IsFalse();
        await Assert.That(check.RevisionIndex).IsEqualTo(-1);
    }

    /// <summary>Checks a range against the sample.</summary>
    /// <param name="range">The range.</param>
    /// <param name="contentsLength">The decoded /Contents length.</param>
    /// <returns>The check.</returns>
    private static PdfByteRangeCheck Check(long[] range, int contentsLength) => PdfByteRangeChecker.Check(range, contentsLength, File, Revisions);
}
