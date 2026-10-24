// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Signatures;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>
/// Tests that a signature container whose last byte is zero is read back whole. The /Contents value is padded with zero
/// bytes, so the container ends where its own DER length says, not where the last non-zero byte is.
/// </summary>
public sealed class ContainerTrailingZeroTests
{
    /// <summary>The most signing attempts made to get a container whose last byte is zero (one in 256 signatures ends that way).</summary>
    private const int MaxAttempts = 20_000;

    /// <summary>A container that ends in a zero byte is extracted with all its bytes and decodes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContainerEndingInZeroKeepsItsLastByte()
    {
        using var certificate = SigningFixtures.CreateEc(ECCurve.NamedCurves.nistP256, "Trailing Zero");
        byte[]? file = null;
        for (var attempt = 0; attempt < MaxAttempts && file is null; attempt++)
        {
            var candidate = SigningFixtures.Sign(SignatureFixtures.Blank(), certificate, PdfSigningOptions.Default);
            if (EndsInZero(candidate))
            {
                file = candidate;
            }
        }

        await Assert.That(file).IsNotNull();
        var covered = SigningFixtures.SignedBytes(file!, out var contents);
        var cms = new SignedCms(new(covered), true);
        cms.Decode(contents);
        cms.CheckSignature(true);

        await Assert.That(contents[^1]).IsEqualTo((byte)0);
    }

    /// <summary>Checks whether the signature container in a file ends with a zero byte.</summary>
    /// <param name="file">The signed file.</param>
    /// <returns><see langword="true"/> when the last byte of the container is zero.</returns>
    private static bool EndsInZero(byte[] file)
    {
        using var document = PdfDocumentReader.Open(file, null);
        var padded = PdfDocumentAttachments.GetSignatures(document)[0].Contents;
        _ = AsnDecoder.ReadEncodedValue(padded, AsnEncodingRules.BER, out _, out _, out var length);
        return padded[length - 1] == 0;
    }
}
