// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace HyperPdf.Cms;

/// <summary>
/// The ECDSA curves and digests ISO/TS 32002:2022, 5.1.3 (Table 3) permits, and whether this computer's cryptography
/// library implements a curve. The NIST curves are everywhere; the Brainpool curves depend on the platform (OpenSSL and
/// Windows CNG provide them, macOS does not).
/// </summary>
internal static class CmsEcCurves
{
    /// <summary>The OID of secp256r1.</summary>
    private const string P256Oid = "1.2.840.10045.3.1.7";

    /// <summary>The OID of secp384r1.</summary>
    private const string P384Oid = "1.3.132.0.34";

    /// <summary>The OID of secp521r1.</summary>
    private const string P521Oid = "1.3.132.0.35";

    /// <summary>The OID of brainpoolP256r1.</summary>
    private const string BrainpoolP256r1Oid = "1.3.36.3.3.2.8.1.1.7";

    /// <summary>The OID of brainpoolP384r1.</summary>
    private const string BrainpoolP384r1Oid = "1.3.36.3.3.2.8.1.1.11";

    /// <summary>The OID of brainpoolP512r1.</summary>
    private const string BrainpoolP512r1Oid = "1.3.36.3.3.2.8.1.1.13";

    /// <summary>The last arc of brainpoolP256r1.</summary>
    private const byte BrainpoolP256r1Arc = 0x07;

    /// <summary>The last arc of brainpoolP384r1.</summary>
    private const byte BrainpoolP384r1Arc = 0x0B;

    /// <summary>The last arc of brainpoolP512r1.</summary>
    private const byte BrainpoolP512r1Arc = 0x0D;

    /// <summary>The bits of a 256-bit field.</summary>
    private const int Bits256 = 256;

    /// <summary>The bits of a 384-bit field.</summary>
    private const int Bits384 = 384;

    /// <summary>The bits of a 512-bit field.</summary>
    private const int Bits512 = 512;

    /// <summary>The bits of the P-521 field.</summary>
    private const int Bits521 = 521;

    /// <summary>The curve count, including the unlisted case.</summary>
    private const int CurveCount = 7;

    /// <summary>The cached value for a curve the platform lacks.</summary>
    private const int Unsupported = 2;

    /// <summary>The cached value for a curve the platform has.</summary>
    private const int Supported = 1;

    /// <summary>The cached platform support: 0 is not tried, 1 is supported, 2 is not.</summary>
    private static readonly int[] Support = new int[CurveCount];

    /// <summary>The digests each curve allows, as a bit per <see cref="CmsDigestAlgorithm"/> value (Table 3).</summary>
    private static readonly ulong[] AllowedDigests =
    [
        0,
        Set(CmsDigestAlgorithm.Sha256, CmsDigestAlgorithm.Sha3_256),
        Set(CmsDigestAlgorithm.Sha384, CmsDigestAlgorithm.Sha3_384),
        Set(CmsDigestAlgorithm.Sha512, CmsDigestAlgorithm.Sha3_512),
        Set(CmsDigestAlgorithm.Sha256, CmsDigestAlgorithm.Sha384, CmsDigestAlgorithm.Sha512, CmsDigestAlgorithm.Sha3_256, CmsDigestAlgorithm.Sha3_384, CmsDigestAlgorithm.Sha3_512),
        Set(CmsDigestAlgorithm.Sha384, CmsDigestAlgorithm.Sha512, CmsDigestAlgorithm.Sha3_384, CmsDigestAlgorithm.Sha3_512),
        Set(CmsDigestAlgorithm.Sha512, CmsDigestAlgorithm.Sha3_512),
    ];

    /// <summary>Gets the encoded secp256r1 OID, 1.2.840.10045.3.1.7.</summary>
    private static ReadOnlySpan<byte> P256Encoded => [0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07];

    /// <summary>Gets the encoded secp384r1 OID, 1.3.132.0.34.</summary>
    private static ReadOnlySpan<byte> P384Encoded => [0x2B, 0x81, 0x04, 0x00, 0x22];

    /// <summary>Gets the encoded secp521r1 OID, 1.3.132.0.35.</summary>
    private static ReadOnlySpan<byte> P521Encoded => [0x2B, 0x81, 0x04, 0x00, 0x23];

    /// <summary>Gets the encoded arcs every Brainpool r1 curve OID starts with, 1.3.36.3.3.2.8.1.1.</summary>
    private static ReadOnlySpan<byte> BrainpoolPrefix => [0x2B, 0x24, 0x03, 0x03, 0x02, 0x08, 0x01, 0x01];

    /// <summary>Maps a named-curve OID to a curve.</summary>
    /// <param name="oid">The curve's OID.</param>
    /// <returns>The curve, or <see cref="CmsEcCurve.Other"/> when the specification does not list it.</returns>
    internal static CmsEcCurve FromOid(string? oid) => oid switch
    {
        P256Oid => CmsEcCurve.P256,
        P384Oid => CmsEcCurve.P384,
        P521Oid => CmsEcCurve.P521,
        BrainpoolP256r1Oid => CmsEcCurve.BrainpoolP256r1,
        BrainpoolP384r1Oid => CmsEcCurve.BrainpoolP384r1,
        BrainpoolP512r1Oid => CmsEcCurve.BrainpoolP512r1,
        _ => CmsEcCurve.Other,
    };

    /// <summary>Maps the encoded contents of a named-curve OID to a curve, without making a string.</summary>
    /// <param name="oid">The OID's content bytes.</param>
    /// <returns>The curve, or <see cref="CmsEcCurve.Other"/> when the specification does not list it.</returns>
    internal static CmsEcCurve FromEncoded(ReadOnlySpan<byte> oid)
    {
        if (oid.SequenceEqual(P256Encoded))
        {
            return CmsEcCurve.P256;
        }

        if (oid.SequenceEqual(P384Encoded))
        {
            return CmsEcCurve.P384;
        }

        if (oid.SequenceEqual(P521Encoded))
        {
            return CmsEcCurve.P521;
        }

        return oid.Length != BrainpoolPrefix.Length + 1 || !oid.StartsWith(BrainpoolPrefix)
            ? CmsEcCurve.Other
            : oid[^1] switch
            {
                BrainpoolP256r1Arc => CmsEcCurve.BrainpoolP256r1,
                BrainpoolP384r1Arc => CmsEcCurve.BrainpoolP384r1,
                BrainpoolP512r1Arc => CmsEcCurve.BrainpoolP512r1,
                _ => CmsEcCurve.Other,
            };
    }

    /// <summary>Gets the size in bits of a curve's field, which is the size .NET reports for a key on it.</summary>
    /// <param name="curve">The curve.</param>
    /// <returns>The bits, or 0 for an unlisted curve.</returns>
    internal static int FieldBits(CmsEcCurve curve) => curve switch
    {
        CmsEcCurve.P256 or CmsEcCurve.BrainpoolP256r1 => Bits256,
        CmsEcCurve.P384 or CmsEcCurve.BrainpoolP384r1 => Bits384,
        CmsEcCurve.P521 => Bits521,
        CmsEcCurve.BrainpoolP512r1 => Bits512,
        _ => 0,
    };

    /// <summary>Gets the OID of a curve.</summary>
    /// <param name="curve">The curve.</param>
    /// <returns>The OID, or an empty string for an unlisted curve.</returns>
    internal static string Oid(CmsEcCurve curve) => curve switch
    {
        CmsEcCurve.P256 => P256Oid,
        CmsEcCurve.P384 => P384Oid,
        CmsEcCurve.P521 => P521Oid,
        CmsEcCurve.BrainpoolP256r1 => BrainpoolP256r1Oid,
        CmsEcCurve.BrainpoolP384r1 => BrainpoolP384r1Oid,
        CmsEcCurve.BrainpoolP512r1 => BrainpoolP512r1Oid,
        _ => string.Empty,
    };

    /// <summary>Gets the name a person reads, such as <c>P-256</c>.</summary>
    /// <param name="curve">The curve.</param>
    /// <returns>The name.</returns>
    internal static string Name(CmsEcCurve curve) => curve switch
    {
        CmsEcCurve.P256 => "P-256",
        CmsEcCurve.P384 => "P-384",
        CmsEcCurve.P521 => "P-521",
        CmsEcCurve.BrainpoolP256r1 => "brainpoolP256r1",
        CmsEcCurve.BrainpoolP384r1 => "brainpoolP384r1",
        CmsEcCurve.BrainpoolP512r1 => "brainpoolP512r1",
        _ => "an unlisted curve",
    };

    /// <summary>
    /// Determines whether Table 3 pairs a curve with a digest. ISO/TS 32001:2022 adds SHAKE256 (RFC 8702, 3.2.2), which
    /// every listed curve accepts.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="digest">The signer's digest.</param>
    /// <returns><see langword="true"/> when the pair is allowed.</returns>
    internal static bool Allows(CmsEcCurve curve, CmsDigestAlgorithm digest) =>
        curve != CmsEcCurve.Other && (digest == CmsDigestAlgorithm.Shake256 || (AllowedDigests[(int)curve] & Bit(digest)) != 0);

    /// <summary>Determines whether this computer's cryptography library implements a curve.</summary>
    /// <param name="curve">The curve.</param>
    /// <returns><see langword="true"/> when it can check signatures on the curve.</returns>
    internal static bool IsSupported(CmsEcCurve curve)
    {
        if (curve == CmsEcCurve.Other)
        {
            return false;
        }

        var known = Volatile.Read(ref Support[(int)curve]);
        if (known != 0)
        {
            return known == Supported;
        }

        var supported = Probe(curve);
        Volatile.Write(ref Support[(int)curve], supported ? Supported : Unsupported);
        return supported;
    }

    /// <summary>Tries to create a key on a curve, the only portable test for a platform's curve support.</summary>
    /// <param name="curve">The curve.</param>
    /// <returns><see langword="true"/> when the platform created the key.</returns>
    private static bool Probe(CmsEcCurve curve)
    {
        try
        {
            using var key = ECDsa.Create(ECCurve.CreateFromValue(Oid(curve)));
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Gets a digest's bit in a set.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>The bit.</returns>
    private static ulong Bit(CmsDigestAlgorithm digest) => 1UL << (int)digest;

    /// <summary>Makes a set of digests.</summary>
    /// <param name="digests">The members.</param>
    /// <returns>The set.</returns>
    private static ulong Set(params ReadOnlySpan<CmsDigestAlgorithm> digests)
    {
        var set = 0UL;
        foreach (var digest in digests)
        {
            set |= Bit(digest);
        }

        return set;
    }
}
