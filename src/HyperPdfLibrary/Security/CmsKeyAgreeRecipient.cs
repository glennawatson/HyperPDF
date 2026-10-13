// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>The parts of a KeyAgreeRecipientInfo (RFC 5652 6.2.2) that name a certificate, sliced from the recipient's bytes.</summary>
/// <param name="originator">The contents of the <c>[0] EXPLICIT</c> OriginatorIdentifierOrKey.</param>
/// <param name="ukm">The user keying material, or an empty span.</param>
/// <param name="keyDerivation">The key encryption algorithm OID's contents: the key agreement and KDF scheme.</param>
/// <param name="wrapAlgorithm">The encoded key wrap AlgorithmIdentifier from the key encryption algorithm's parameters.</param>
/// <param name="encryptedKey">The wrapped content-encryption key for the certificate.</param>
/// <param name="encryptedContentInfo">The envelope's EncryptedContentInfo contents.</param>
internal readonly ref struct CmsKeyAgreeRecipient(
    ReadOnlySpan<byte> originator,
    ReadOnlySpan<byte> ukm,
    ReadOnlySpan<byte> keyDerivation,
    ReadOnlySpan<byte> wrapAlgorithm,
    ReadOnlySpan<byte> encryptedKey,
    ReadOnlySpan<byte> encryptedContentInfo)
{
    /// <summary>Gets the contents of the <c>[0] EXPLICIT</c> OriginatorIdentifierOrKey.</summary>
    internal ReadOnlySpan<byte> Originator { get; } = originator;

    /// <summary>Gets the user keying material, or an empty span.</summary>
    internal ReadOnlySpan<byte> Ukm { get; } = ukm;

    /// <summary>Gets the key encryption algorithm OID's contents.</summary>
    internal ReadOnlySpan<byte> KeyDerivation { get; } = keyDerivation;

    /// <summary>Gets the encoded key wrap AlgorithmIdentifier.</summary>
    internal ReadOnlySpan<byte> WrapAlgorithm { get; } = wrapAlgorithm;

    /// <summary>Gets the wrapped content-encryption key.</summary>
    internal ReadOnlySpan<byte> EncryptedKey { get; } = encryptedKey;

    /// <summary>Gets the envelope's EncryptedContentInfo contents.</summary>
    internal ReadOnlySpan<byte> EncryptedContentInfo { get; } = encryptedContentInfo;
}
