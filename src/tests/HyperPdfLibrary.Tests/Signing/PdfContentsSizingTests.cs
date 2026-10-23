// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Signing;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>Checks signature-reservation arithmetic before allocating file buffers.</summary>
public sealed class PdfContentsSizingTests
{
    /// <summary>The reservation alignment.</summary>
    private const int Alignment = 64;

    /// <summary>The next aligned reservation.</summary>
    private const int DoubleAlignment = Alignment * 2;

    /// <summary>The synthetic RSA modulus size.</summary>
    private const int KeyBits = 2048;

    /// <summary>Gets a sized key without requiring a platform certificate parser.</summary>
    private static PdfSignerCertificate Signer => new([], default, default, default, PdfSignerKeyKind.Rsa, default, KeyBits);

    /// <summary>Unrepresentable and negative reservations report a domain error.</summary>
    /// <param name="size">The requested reservation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(int.MinValue)]
    [Arguments(int.MaxValue)]
    public async Task RoundUp_InvalidSize_ThrowsDomainError(int size) =>
        await Assert.That(() => PdfContentsSizing.RoundUp(size)).Throws<PdfException>();

    /// <summary>Normal reservations retain their alignment.</summary>
    /// <param name="size">The requested reservation.</param>
    /// <param name="expected">The aligned size.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0, 0)]
    [Arguments(1, Alignment)]
    [Arguments(Alignment, Alignment)]
    [Arguments(Alignment + 1, DoubleAlignment)]
    public async Task RoundUp_NormalSize_PreservesAlignment(int size, int expected) =>
        await Assert.That(PdfContentsSizing.RoundUp(size)).IsEqualTo(expected);

    /// <summary>Headroom cannot overflow the sum into a small or negative reservation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Estimate_ExcessiveHeadroom_ThrowsDomainError() =>
        await Assert.That(static () => PdfContentsSizing.Estimate([], Signer, 0, false, int.MaxValue)).Throws<PdfException>();

    /// <summary>A timestamp estimate cannot overflow the reservation arithmetic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Estimate_ExcessiveTimestamp_ThrowsDomainError() =>
        await Assert.That(static () => PdfContentsSizing.Estimate([], Signer, int.MaxValue, true, 0)).Throws<PdfException>();

    /// <summary>Large accumulated lengths fail before they are narrowed to an integer.</summary>
    /// <param name="size">The accumulated reservation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(long.MinValue)]
    [Arguments(long.MaxValue)]
    public async Task RoundUp_InvalidAccumulatedSize_ThrowsDomainError(long size) =>
        await Assert.That(() => PdfContentsSizing.RoundUp(size)).Throws<PdfException>();

    /// <summary>Retry headroom cannot overflow the signature's actual length.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Grow_ExcessiveHeadroom_ThrowsDomainError() =>
        await Assert.That(static () => PdfSigningEngine.Grow(Alignment, int.MaxValue)).Throws<PdfException>();
}
