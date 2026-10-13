// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Cms;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>Tests for the Ed25519 verification of RFC 8032, 5.1.7 against the test vectors of RFC 8032, 7.1.</summary>
public sealed class EdDsaTests
{
    /// <summary>RFC 8032, 7.1, test 1: the public key.</summary>
    private const string Test1Key = "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a";

    /// <summary>RFC 8032, 7.1, test 1: the signature of the empty message.</summary>
    private const string Test1Signature = "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b";

    /// <summary>RFC 8032, 7.1, test 2: the public key.</summary>
    private const string Test2Key = "3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c";

    /// <summary>RFC 8032, 7.1, test 2: the signature of the one-byte message 72.</summary>
    private const string Test2Signature = "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00";

    /// <summary>The first byte of the one-byte message of test 2.</summary>
    private const byte Test2Message = 0x72;

    /// <summary>The offset of the S half in an Ed25519 signature.</summary>
    private const int SignatureHalf = 32;

    /// <summary>The last byte of S, whose top bits make S larger than the group order.</summary>
    private const int SignatureLast = 63;

    /// <summary>The RFC 8032 test vectors verify.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Rfc8032VectorsVerify()
    {
        await Assert.That(EdDsa.Verify(EdwardsCurve.Ed25519, Convert.FromHexString(Test1Key), [], Convert.FromHexString(Test1Signature))).IsTrue();
        await Assert.That(EdDsa.Verify(EdwardsCurve.Ed25519, Convert.FromHexString(Test2Key), [Test2Message], Convert.FromHexString(Test2Signature))).IsTrue();
    }

    /// <summary>A changed message, signature or key does not verify.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangedInputsDoNotVerify()
    {
        var key = Convert.FromHexString(Test2Key);
        var signature = Convert.FromHexString(Test2Signature);
        await Assert.That(EdDsa.Verify(EdwardsCurve.Ed25519, key, [Test2Message + 1], signature)).IsFalse();

        var changedR = signature.ToArray();
        changedR[0] ^= 1;
        await Assert.That(EdDsa.Verify(EdwardsCurve.Ed25519, key, [Test2Message], changedR)).IsFalse();

        var changedS = signature.ToArray();
        changedS[SignatureHalf] ^= 1;
        await Assert.That(EdDsa.Verify(EdwardsCurve.Ed25519, key, [Test2Message], changedS)).IsFalse();

        var otherKey = Convert.FromHexString(Test1Key);
        await Assert.That(EdDsa.Verify(EdwardsCurve.Ed25519, otherKey, [Test2Message], signature)).IsFalse();
    }

    /// <summary>An S at or above the group order is refused, as RFC 8032, 5.1.7 requires.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScalarAboveTheOrderIsRefused()
    {
        var signature = Convert.FromHexString(Test1Signature);
        signature[SignatureLast] = 0xFF;
        await Assert.That(EdDsa.Verify(EdwardsCurve.Ed25519, Convert.FromHexString(Test1Key), [], signature)).IsFalse();
    }

    /// <summary>A key whose y is not below the prime is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonCanonicalKeyIsRefused()
    {
        var key = new byte[SignatureHalf];
        Array.Fill(key, (byte)0xFF);
        key[SignatureHalf - 1] = 0x7F;
        await Assert.That(EdDsa.Verify(EdwardsCurve.Ed25519, key, [], Convert.FromHexString(Test1Signature))).IsFalse();
    }
}
