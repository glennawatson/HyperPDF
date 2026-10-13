// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace HyperPdfLibrary.Security;

/// <summary>Maps the CMS algorithm identifiers of an ECDH recipient to the parameters the reader supports.</summary>
internal static class CmsAlgorithms
{
    /// <summary>The bytes of a 128-bit AES key.</summary>
    internal const int Aes128KeyLength = 16;

    /// <summary>The bytes of a 192-bit AES key.</summary>
    internal const int Aes192KeyLength = 24;

    /// <summary>The bytes of a 256-bit AES key.</summary>
    internal const int Aes256KeyLength = 32;

    /// <summary>The last arc of <c>aes128-CBC</c>.</summary>
    private const byte Aes128Cbc = 2;

    /// <summary>The last arc of <c>aes192-CBC</c>.</summary>
    private const byte Aes192Cbc = 22;

    /// <summary>The last arc of <c>aes256-CBC</c>.</summary>
    private const byte Aes256Cbc = 42;

    /// <summary>The last arc of <c>id-aes128-wrap</c>.</summary>
    private const byte Aes128Wrap = 5;

    /// <summary>The last arc of <c>id-aes192-wrap</c>.</summary>
    private const byte Aes192Wrap = 25;

    /// <summary>The last arc of <c>id-aes256-wrap</c>.</summary>
    private const byte Aes256Wrap = 45;

    /// <summary>The last arc of the SEC 1 schemes that use SHA-256.</summary>
    private const byte Sha256Arc = 1;

    /// <summary>The last arc of the SEC 1 schemes that use SHA-384.</summary>
    private const byte Sha384Arc = 2;

    /// <summary>The last arc of the SEC 1 schemes that use SHA-512.</summary>
    private const byte Sha512Arc = 3;

    /// <summary>Gets the key length of an AES-CBC content cipher (RFC 3565).</summary>
    /// <param name="oid">The algorithm OID's contents.</param>
    /// <returns>The key length in bytes, or zero when the cipher is not AES-CBC.</returns>
    internal static int AesCbcKeyLength(ReadOnlySpan<byte> oid) => AesArc(oid) switch
    {
        Aes128Cbc => Aes128KeyLength,
        Aes192Cbc => Aes192KeyLength,
        Aes256Cbc => Aes256KeyLength,
        _ => 0,
    };

    /// <summary>Gets the key length of an AES key wrap (RFC 3394, RFC 3565 2.3.2).</summary>
    /// <param name="oid">The algorithm OID's contents.</param>
    /// <returns>The key length in bytes, or zero when the algorithm is not an AES key wrap.</returns>
    internal static int AesWrapKeyLength(ReadOnlySpan<byte> oid) => AesArc(oid) switch
    {
        Aes128Wrap => Aes128KeyLength,
        Aes192Wrap => Aes192KeyLength,
        Aes256Wrap => Aes256KeyLength,
        _ => 0,
    };

    /// <summary>
    /// Gets the X9.63 KDF hash of an ephemeral-static ECDH key agreement scheme (RFC 5753 7.1.4). The cofactor variants
    /// give the same shared secret as the standard ones on curves with cofactor 1, which <see cref="IsCofactorOne"/> checks.
    /// SHA-224 schemes and MQV are not supported because .NET has no SHA-224 and no MQV.
    /// </summary>
    /// <param name="oid">The key encryption algorithm OID's contents.</param>
    /// <param name="cofactor">Whether the scheme is a cofactor variant.</param>
    /// <returns>The hash, or a default name when the scheme is not supported.</returns>
    internal static HashAlgorithmName KeyDerivationHash(ReadOnlySpan<byte> oid, out bool cofactor)
    {
        cofactor = oid.SequenceEqual(CmsOids.CofactorDhSha1) || IsArcOf(oid, CmsOids.CofactorDhPrefix);
        if (oid.SequenceEqual(CmsOids.StandardDhSha1) || oid.SequenceEqual(CmsOids.CofactorDhSha1))
        {
            return HashAlgorithmName.SHA1;
        }

        return IsArcOf(oid, CmsOids.StandardDhPrefix) || cofactor ? Sec1Hash(oid[^1]) : default;
    }

    /// <summary>Determines whether a named curve has cofactor 1, so cofactor and standard ECDH agree.</summary>
    /// <param name="curve">The recipient key's curve.</param>
    /// <returns><see langword="true"/> for the NIST prime curves, secp256k1 and the brainpool r1 curves.</returns>
    internal static bool IsCofactorOne(ECCurve curve) => curve.IsNamed && curve.Oid.Value is
        "1.2.840.10045.3.1.7" or "1.3.132.0.33" or "1.3.132.0.34" or "1.3.132.0.35" or "1.3.132.0.10"
        or "1.3.36.3.3.2.8.1.1.7" or "1.3.36.3.3.2.8.1.1.11" or "1.3.36.3.3.2.8.1.1.13";

    /// <summary>Gets the hash a SEC 1 key agreement scheme's last arc names.</summary>
    /// <param name="arc">The last arc.</param>
    /// <returns>The hash, or a default name for SHA-224 or an unknown arc.</returns>
    private static HashAlgorithmName Sec1Hash(byte arc) => arc switch
    {
        Sha256Arc => HashAlgorithmName.SHA256,
        Sha384Arc => HashAlgorithmName.SHA384,
        Sha512Arc => HashAlgorithmName.SHA512,
        _ => default,
    };

    /// <summary>Gets the last arc of an OID under the NIST AES arc.</summary>
    /// <param name="oid">The OID's contents.</param>
    /// <returns>The arc, or zero for another OID.</returns>
    private static byte AesArc(ReadOnlySpan<byte> oid) => IsArcOf(oid, CmsOids.AesPrefix) ? oid[^1] : (byte)0;

    /// <summary>Determines whether an OID is a single-byte arc directly under a prefix.</summary>
    /// <param name="oid">The OID's contents.</param>
    /// <param name="prefix">The prefix's contents.</param>
    /// <returns><see langword="true"/> when the OID is the prefix plus one arc below 128.</returns>
    private static bool IsArcOf(ReadOnlySpan<byte> oid, ReadOnlySpan<byte> prefix) =>
        oid.Length == prefix.Length + 1 && oid.StartsWith(prefix) && oid[^1] < 0x80;
}
