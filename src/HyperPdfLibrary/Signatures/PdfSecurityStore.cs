// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// The document security store (/DSS, PAdES long-term validation): certificates, OCSP responses and CRLs saved in the
/// file, and per-signature /VRI entries.
/// </summary>
/// <param name="Certificates">The DER certificates.</param>
/// <param name="OcspResponses">The DER OCSP responses.</param>
/// <param name="Crls">The DER certificate revocation lists.</param>
/// <param name="Vri">The /VRI entries by key.</param>
[DebuggerDisplay("PdfSecurityStore: {Certificates.Length} certificates, {OcspResponses.Length} OCSP, {Crls.Length} CRLs")]
public sealed record PdfSecurityStore(
    byte[][] Certificates,
    byte[][] OcspResponses,
    byte[][] Crls,
    IReadOnlyDictionary<string, PdfValidationRelatedInfo> Vri)
{
    /// <summary>The length of a SHA-1 hash.</summary>
    private const int Sha1Length = 20;

    /// <summary>Gets an empty store, for files without one.</summary>
    public static PdfSecurityStore Empty { get; } = new(
        [],
        [],
        [],
        new ReadOnlyDictionary<string, PdfValidationRelatedInfo>(new Dictionary<string, PdfValidationRelatedInfo>(StringComparer.Ordinal)));

    /// <summary>Gets a value indicating whether the store holds revocation data.</summary>
    public bool HasRevocationData => OcspResponses.Length + Crls.Length > 0;

    /// <summary>
    /// Finds the /VRI entry for a signature: the key is the SHA-1 of its /Contents, which writers take either with the
    /// zero padding or over the DER signature alone, so both are tried.
    /// </summary>
    /// <param name="contents">The signature's /Contents bytes, with any padding.</param>
    /// <param name="encodedLength">The length of the DER signature inside them.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    public PdfValidationRelatedInfo? FindVri(ReadOnlySpan<byte> contents, int encodedLength)
    {
        if (Vri.Count == 0)
        {
            return null;
        }

        // PAdES defines the key as a SHA-1 hash, so the algorithm is fixed by the format.
        Span<byte> digest = stackalloc byte[Sha1Length];
        _ = CryptographicOperations.HashData(HashAlgorithmName.SHA1, contents, digest);
        if (Vri.TryGetValue(Convert.ToHexString(digest), out var padded))
        {
            return padded;
        }

        if ((uint)encodedLength >= (uint)contents.Length)
        {
            return null;
        }

        _ = CryptographicOperations.HashData(HashAlgorithmName.SHA1, contents[..encodedLength], digest);
        return Vri.GetValueOrDefault(Convert.ToHexString(digest));
    }
}
