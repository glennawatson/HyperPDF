// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>
/// A signature that is prepared and waits for its signature value, for a signer that works apart from the app. It holds the
/// finished file with room reserved for the signature, so the open document can change or close before the signer answers.
/// </summary>
public interface IPendingSignature
{
    /// <summary>Gets the DER-encoded signed attributes: the bytes a signer hashes with <see cref="Digest"/> and signs.</summary>
    ReadOnlyMemory<byte> SignedAttributes { get; }

    /// <summary>Gets the digest of the signed bytes of the file; a signing service that builds the whole container signs this.</summary>
    ReadOnlyMemory<byte> DocumentDigest { get; }

    /// <summary>Gets the digest to hash <see cref="SignedAttributes"/> with.</summary>
    SigningDigest Digest { get; }

    /// <summary>Gets the format of the signature.</summary>
    SigningFormat Format { get; }

    /// <summary>Gets the fully qualified name of the signature field.</summary>
    string FieldName { get; }

    /// <summary>Gets the room reserved for the signature value, in bytes.</summary>
    int ContentsCapacity { get; }
}
