// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Cms;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Signing;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>Checks exclusively leased signing digest contexts and failure cleanup.</summary>
public sealed class PdfSigningDigestsTests
{
    /// <summary>Gets the first synthetic signed range.</summary>
    private static ReadOnlySpan<byte> First => "first"u8;

    /// <summary>Gets the second synthetic signed range.</summary>
    private static ReadOnlySpan<byte> Second => "second"u8;

    /// <summary>Two successive ranges hash as their concatenation on every supported algorithm.</summary>
    /// <param name="digest">The digest algorithm.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments((int)CmsDigestAlgorithm.Sha1)]
    [Arguments((int)CmsDigestAlgorithm.Sha256)]
    [Arguments((int)CmsDigestAlgorithm.Sha384)]
    [Arguments((int)CmsDigestAlgorithm.Sha512)]
    [Arguments((int)CmsDigestAlgorithm.Sha3_256)]
    [Arguments((int)CmsDigestAlgorithm.Sha3_384)]
    [Arguments((int)CmsDigestAlgorithm.Sha3_512)]
    [Arguments((int)CmsDigestAlgorithm.Shake256)]
    public async Task Hash_TwoRanges_MatchesConcatenatedInput(int digest, CancellationToken cancellationToken)
    {
        var algorithm = (CmsDigestAlgorithm)digest;
        var owner = new ScratchPools();
        try
        {
            var expected = new byte[CmsDigests.MaxLength];
            var actual = new byte[CmsDigests.MaxLength];
            var expectedLength = CmsDigests.Hash(algorithm, "firstsecond"u8, expected);
            var length = PdfSigningDigests.Hash(algorithm, First, Second, actual, owner, cancellationToken);
            var repeated = PdfSigningDigests.Hash(algorithm, First, Second, actual, owner, cancellationToken);

            await Assert.That(length).IsEqualTo(expectedLength);
            await Assert.That(repeated).IsEqualTo(expectedLength);
            await Assert.That(actual.AsSpan(0, length).SequenceEqual(expected.AsSpan(0, length))).IsTrue();
        }
        finally
        {
            owner.Close();
        }
    }

    /// <summary>One caller reuses its context without borrowing another caller's state.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Hash_DifferentOwners_KeepSeparateContexts(CancellationToken cancellationToken)
    {
        var first = new ScratchPools();
        var second = new ScratchPools();
        try
        {
            var destination = new byte[CmsDigests.MaxLength];
            _ = PdfSigningDigests.Hash(CmsDigestAlgorithm.Sha256, First, [], destination, first, cancellationToken);
            _ = PdfSigningDigests.Hash(CmsDigestAlgorithm.Sha256, Second, [], destination, second, cancellationToken);
            var firstPool = first.Objects<PdfSigningHashState>();
            var secondPool = second.Objects<PdfSigningHashState>();
            var firstState = firstPool.Take()!;
            var secondState = secondPool.Take()!;
            var distinct = !ReferenceEquals(firstState, secondState);
            var keptFirst = firstPool.Offer(firstState);
            var keptSecond = secondPool.Offer(secondState);
            _ = PdfSigningDigests.Hash(CmsDigestAlgorithm.Sha256, First, [], destination, first, cancellationToken);
            var reused = firstPool.Take()!;
            var same = ReferenceEquals(firstState, reused);
            reused.Dispose();

            await Assert.That(distinct).IsTrue();
            await Assert.That(keptFirst && keptSecond).IsTrue();
            await Assert.That(same).IsTrue();
        }
        finally
        {
            first.Close();
            second.Close();
        }
    }

    /// <summary>A failed digest is disposed instead of contaminating the next hash.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Hash_ShortDestination_DropsPartiallyFedContext(CancellationToken cancellationToken)
    {
        var owner = new ScratchPools();
        try
        {
            await Assert.That(() => PdfSigningDigests.Hash(CmsDigestAlgorithm.Sha256, First, Second, new byte[1], owner, cancellationToken))
                .Throws<ArgumentException>();
            var idle = owner.Objects<PdfSigningHashState>().Take();
            idle?.Dispose();
            var actual = new byte[CmsDigests.MaxLength];
            var expected = new byte[CmsDigests.MaxLength];
            var length = PdfSigningDigests.Hash(CmsDigestAlgorithm.Sha256, "clean"u8, [], actual, owner, cancellationToken);
            var expectedLength = CmsDigests.Hash(CmsDigestAlgorithm.Sha256, "clean"u8, expected);

            await Assert.That(idle).IsNull();
            await Assert.That(length).IsEqualTo(expectedLength);
            await Assert.That(actual).IsEquivalentTo(expected);
        }
        finally
        {
            owner.Close();
        }
    }

    /// <summary>A closed caller never retains a newly used native context.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Hash_ClosedOwner_DoesNotRetainContext(CancellationToken cancellationToken)
    {
        var owner = new ScratchPools();
        var pool = owner.Objects<PdfSigningHashState>();
        owner.Close();
        var length = PdfSigningDigests.Hash(CmsDigestAlgorithm.Sha256, First, [], new byte[CmsDigests.MaxLength], owner, cancellationToken);
        var idle = pool.Take();
        idle?.Dispose();

        await Assert.That(length).IsGreaterThan(0);
        await Assert.That(idle).IsNull();
    }

    /// <summary>Cancellation does not register or lease any hash context.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Hash_Cancelled_DoesNotRetainContext()
    {
        var owner = new ScratchPools();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        try
        {
            await Assert.That(() => PdfSigningDigests.Hash(CmsDigestAlgorithm.Sha256, First, [], new byte[CmsDigests.MaxLength], owner, cancelled.Token))
                .Throws<OperationCanceledException>();
            var idle = owner.Objects<PdfSigningHashState>().Take();
            idle?.Dispose();

            await Assert.That(idle).IsNull();
        }
        finally
        {
            owner.Close();
        }
    }
}
