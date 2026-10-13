// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace HyperPdf.Cms;

/// <summary>
/// Maps CMS digest algorithm identifiers to digests, checks the SHA-3 and SHAKE256 rules of ISO/TS 32001:2022 (5.1.4,
/// with its errata applying RFC 8702, 3.1 and RFC 8419, 3.1 and 3.2), and computes the digests where this computer can.
/// </summary>
internal static class CmsDigests
{
    /// <summary>The longest digest produced: SHA-512, SHA3-512 and SHAKE256 all give 64 bytes.</summary>
    internal const int MaxLength = 64;

    /// <summary>The SHA-1 OID.</summary>
    internal const string Sha1Oid = "1.3.14.3.2.26";

    /// <summary>The SHA-256 OID.</summary>
    internal const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

    /// <summary>The SHA-384 OID.</summary>
    internal const string Sha384Oid = "2.16.840.1.101.3.4.2.2";

    /// <summary>The SHA-512 OID.</summary>
    internal const string Sha512Oid = "2.16.840.1.101.3.4.2.3";

    /// <summary>The SHA3-256 OID.</summary>
    internal const string Sha3_256Oid = "2.16.840.1.101.3.4.2.8";

    /// <summary>The SHA3-384 OID.</summary>
    internal const string Sha3_384Oid = "2.16.840.1.101.3.4.2.9";

    /// <summary>The SHA3-512 OID.</summary>
    internal const string Sha3_512Oid = "2.16.840.1.101.3.4.2.10";

    /// <summary>The id-shake128 OID.</summary>
    internal const string Shake128Oid = "2.16.840.1.101.3.4.2.11";

    /// <summary>The id-shake256 OID, which fixes the output at 512 bits (RFC 8702, 3.1).</summary>
    internal const string Shake256Oid = "2.16.840.1.101.3.4.2.12";

    /// <summary>The id-shake128-len OID.</summary>
    internal const string Shake128LenOid = "2.16.840.1.101.3.4.2.17";

    /// <summary>The id-shake256-len OID, whose parameter gives the output length in bits (RFC 8419, 2.3).</summary>
    internal const string Shake256LenOid = "2.16.840.1.101.3.4.2.18";

    /// <summary>The most bytes the encoded contents of a digest OID take.</summary>
    internal const int MaxEncodedOidLength = 9;

    /// <summary>The SHA-1 length.</summary>
    private const int Sha1Length = 20;

    /// <summary>The SHA-256 and SHA3-256 length.</summary>
    private const int Length256 = 32;

    /// <summary>The SHA-384 and SHA3-384 length.</summary>
    private const int Length384 = 48;

    /// <summary>The only SHAKE256 output length PDF allows, in bits.</summary>
    private const int Shake256Bits = 512;

    /// <summary>The last arc of the NIST hash algorithms.</summary>
    private const byte Sha256Arc = 0x01;

    /// <summary>The last arc of SHA-384.</summary>
    private const byte Sha384Arc = 0x02;

    /// <summary>The last arc of SHA-512.</summary>
    private const byte Sha512Arc = 0x03;

    /// <summary>The last arc of SHA3-256.</summary>
    private const byte Sha3_256Arc = 0x08;

    /// <summary>The last arc of SHA3-384.</summary>
    private const byte Sha3_384Arc = 0x09;

    /// <summary>The last arc of SHA3-512.</summary>
    private const byte Sha3_512Arc = 0x0A;

    /// <summary>The last arc of id-shake128.</summary>
    private const byte Shake128Arc = 0x0B;

    /// <summary>The last arc of id-shake256.</summary>
    private const byte Shake256Arc = 0x0C;

    /// <summary>The last arc of id-shake128-len.</summary>
    private const byte Shake128LenArc = 0x11;

    /// <summary>The last arc of id-shake256-len.</summary>
    private const byte Shake256LenArc = 0x12;

    /// <summary>Gets the encoded arcs every NIST hash algorithm OID starts with, 2.16.840.1.101.3.4.2.</summary>
    private static ReadOnlySpan<byte> NistHashPrefix => [0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02];

    /// <summary>Gets the encoded SHA-1 OID.</summary>
    private static ReadOnlySpan<byte> Sha1Encoded => [0x2B, 0x0E, 0x03, 0x02, 0x1A];

    /// <summary>Maps a digest OID to its algorithm, ignoring parameters.</summary>
    /// <param name="oid">The OID.</param>
    /// <returns>The algorithm, or <see cref="CmsDigestAlgorithm.Unknown"/>.</returns>
    internal static CmsDigestAlgorithm FromOid(string? oid) => oid switch
    {
        Sha1Oid => CmsDigestAlgorithm.Sha1,
        Sha256Oid => CmsDigestAlgorithm.Sha256,
        Sha384Oid => CmsDigestAlgorithm.Sha384,
        Sha512Oid => CmsDigestAlgorithm.Sha512,
        Sha3_256Oid => CmsDigestAlgorithm.Sha3_256,
        Sha3_384Oid => CmsDigestAlgorithm.Sha3_384,
        Sha3_512Oid => CmsDigestAlgorithm.Sha3_512,
        Shake256Oid or Shake256LenOid => CmsDigestAlgorithm.Shake256,
        Shake128Oid or Shake128LenOid => CmsDigestAlgorithm.Shake128,
        _ => CmsDigestAlgorithm.Unknown,
    };

    /// <summary>Gets the OID a signer writes for an algorithm.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns>The OID, or an empty string for an unknown algorithm.</returns>
    internal static string Oid(CmsDigestAlgorithm algorithm) => algorithm switch
    {
        CmsDigestAlgorithm.Sha1 => Sha1Oid,
        CmsDigestAlgorithm.Sha256 => Sha256Oid,
        CmsDigestAlgorithm.Sha384 => Sha384Oid,
        CmsDigestAlgorithm.Sha512 => Sha512Oid,
        CmsDigestAlgorithm.Sha3_256 => Sha3_256Oid,
        CmsDigestAlgorithm.Sha3_384 => Sha3_384Oid,
        CmsDigestAlgorithm.Sha3_512 => Sha3_512Oid,
        CmsDigestAlgorithm.Shake256 => Shake256Oid,
        CmsDigestAlgorithm.Shake128 => Shake128Oid,
        _ => string.Empty,
    };

    /// <summary>Writes the encoded contents of the OID a signer writes for an algorithm.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="destination">A buffer of at least <see cref="MaxEncodedOidLength"/> bytes.</param>
    /// <returns>The bytes written, or 0 for an unknown algorithm.</returns>
    internal static int EncodeOid(CmsDigestAlgorithm algorithm, Span<byte> destination)
    {
        if (algorithm == CmsDigestAlgorithm.Sha1)
        {
            Sha1Encoded.CopyTo(destination);
            return Sha1Encoded.Length;
        }

        var arc = algorithm switch
        {
            CmsDigestAlgorithm.Sha256 => Sha256Arc,
            CmsDigestAlgorithm.Sha384 => Sha384Arc,
            CmsDigestAlgorithm.Sha512 => Sha512Arc,
            CmsDigestAlgorithm.Sha3_256 => Sha3_256Arc,
            CmsDigestAlgorithm.Sha3_384 => Sha3_384Arc,
            CmsDigestAlgorithm.Sha3_512 => Sha3_512Arc,
            CmsDigestAlgorithm.Shake256 => Shake256Arc,
            CmsDigestAlgorithm.Shake128 => Shake128Arc,
            _ => (byte)0,
        };
        if (arc == 0)
        {
            return 0;
        }

        NistHashPrefix.CopyTo(destination);
        destination[NistHashPrefix.Length] = arc;
        return NistHashPrefix.Length + 1;
    }

    /// <summary>Determines whether an OID is a SHAKE identifier, whose parameters decide whether PDF allows it.</summary>
    /// <param name="oid">The OID.</param>
    /// <returns><see langword="true"/> for id-shake128, id-shake256 and their -len forms.</returns>
    internal static bool IsShake(string? oid) => FromOid(oid) is CmsDigestAlgorithm.Shake256 or CmsDigestAlgorithm.Shake128;

    /// <summary>Gets whether an algorithm is allowed and can be computed here, without looking at its parameters.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns>The status.</returns>
    internal static CmsDigestStatus Status(CmsDigestAlgorithm algorithm) => algorithm switch
    {
        CmsDigestAlgorithm.Unknown => CmsDigestStatus.Unknown,
        CmsDigestAlgorithm.Shake128 => CmsDigestStatus.NotAllowed,
        CmsDigestAlgorithm.Sha3_256 => Available(SHA3_256.IsSupported),
        CmsDigestAlgorithm.Sha3_384 => Available(SHA3_384.IsSupported),
        CmsDigestAlgorithm.Sha3_512 => Available(SHA3_512.IsSupported),
        CmsDigestAlgorithm.Shake256 => Available(Shake256.IsSupported),
        _ => CmsDigestStatus.Available,
    };

    /// <summary>
    /// Reads a DER AlgorithmIdentifier and checks its parameters: id-shake256 must have none and id-shake256-len must
    /// give 512 bits; SHAKE128 in any form is not allowed.
    /// </summary>
    /// <param name="identifier">The encoded AlgorithmIdentifier.</param>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns>The status.</returns>
    internal static CmsDigestStatus ReadIdentifier(ReadOnlySpan<byte> identifier, out CmsDigestAlgorithm algorithm)
    {
        algorithm = CmsDigestAlgorithm.Unknown;
        if (!CmsDer.TryReadChild(identifier, Asn1Tag.Sequence, out var body)
            || !CmsDer.TryRead(body, out var oidTag, out var oid, out var used)
            || oidTag != Asn1Tag.ObjectIdentifier)
        {
            return CmsDigestStatus.Unknown;
        }

        var parameters = body[used..];
        algorithm = FromEncodedOid(oid, out var arc);
        return algorithm switch
        {
            CmsDigestAlgorithm.Unknown => CmsDigestStatus.Unknown,
            CmsDigestAlgorithm.Shake256 when !Shake256ParametersAllowed(arc, parameters) => CmsDigestStatus.NotAllowed,
            _ => Status(algorithm),
        };
    }

    /// <summary>
    /// Determines whether an AlgorithmIdentifier is id-shake256 with no parameters, the only SHAKE256 form Ed448 uses
    /// (ISO/TS 32002:2022, Table 4).
    /// </summary>
    /// <param name="identifier">The encoded AlgorithmIdentifier.</param>
    /// <returns><see langword="true"/> for the fixed-length form.</returns>
    internal static bool IsFixedShake256(ReadOnlySpan<byte> identifier) =>
        CmsDer.TryReadChild(identifier, Asn1Tag.Sequence, out var body)
        && CmsDer.TryRead(body, out var tag, out var oid, out var used)
        && tag == Asn1Tag.ObjectIdentifier
        && used == body.Length
        && FromEncodedOid(oid, out var arc) == CmsDigestAlgorithm.Shake256
        && arc == Shake256Arc;

    /// <summary>Maps the content bytes of an encoded digest OID to its algorithm, ignoring parameters.</summary>
    /// <param name="oid">The OID's content bytes.</param>
    /// <returns>The algorithm, or <see cref="CmsDigestAlgorithm.Unknown"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CmsDigestAlgorithm FromEncoded(ReadOnlySpan<byte> oid) => FromEncodedOid(oid, out _);

    /// <summary>Gets an algorithm's digest length.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns>The length in bytes, or 0 for an unknown algorithm.</returns>
    internal static int Length(CmsDigestAlgorithm algorithm) => algorithm switch
    {
        CmsDigestAlgorithm.Sha1 => Sha1Length,
        CmsDigestAlgorithm.Sha256 or CmsDigestAlgorithm.Sha3_256 => Length256,
        CmsDigestAlgorithm.Sha384 or CmsDigestAlgorithm.Sha3_384 => Length384,
        CmsDigestAlgorithm.Sha512 or CmsDigestAlgorithm.Sha3_512 or CmsDigestAlgorithm.Shake256 => MaxLength,
        _ => 0,
    };

    /// <summary>Gets the name a person reads, such as <c>SHA3-256</c>.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns>The name.</returns>
    internal static string Name(CmsDigestAlgorithm algorithm) => algorithm switch
    {
        CmsDigestAlgorithm.Sha1 => "SHA-1",
        CmsDigestAlgorithm.Sha256 => "SHA-256",
        CmsDigestAlgorithm.Sha384 => "SHA-384",
        CmsDigestAlgorithm.Sha512 => "SHA-512",
        CmsDigestAlgorithm.Sha3_256 => "SHA3-256",
        CmsDigestAlgorithm.Sha3_384 => "SHA3-384",
        CmsDigestAlgorithm.Sha3_512 => "SHA3-512",
        CmsDigestAlgorithm.Shake256 => "SHAKE256",
        CmsDigestAlgorithm.Shake128 => "SHAKE128",
        _ => "an unknown digest",
    };

    /// <summary>Gets the hash algorithm name .NET signs and verifies with; SHAKE has none.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns>The name, or a default name for SHAKE and unknown algorithms.</returns>
    internal static HashAlgorithmName HashName(CmsDigestAlgorithm algorithm) => algorithm switch
    {
        CmsDigestAlgorithm.Sha1 => HashAlgorithmName.SHA1,
        CmsDigestAlgorithm.Sha256 => HashAlgorithmName.SHA256,
        CmsDigestAlgorithm.Sha384 => HashAlgorithmName.SHA384,
        CmsDigestAlgorithm.Sha512 => HashAlgorithmName.SHA512,
        CmsDigestAlgorithm.Sha3_256 => HashAlgorithmName.SHA3_256,
        CmsDigestAlgorithm.Sha3_384 => HashAlgorithmName.SHA3_384,
        CmsDigestAlgorithm.Sha3_512 => HashAlgorithmName.SHA3_512,
        _ => default,
    };

    /// <summary>Hashes data, when the algorithm is available here.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="data">The data.</param>
    /// <param name="destination">A buffer of at least <see cref="MaxLength"/> bytes.</param>
    /// <returns>The number of bytes written, or 0 when the algorithm is unknown, not allowed or not available.</returns>
    internal static int Hash(CmsDigestAlgorithm algorithm, ReadOnlySpan<byte> data, Span<byte> destination)
    {
        if (Status(algorithm) != CmsDigestStatus.Available)
        {
            return 0;
        }

        if (algorithm == CmsDigestAlgorithm.Shake256)
        {
            Shake256.HashData(data, destination[..MaxLength]);
            return MaxLength;
        }

        return CryptographicOperations.HashData(HashName(algorithm), data, destination);
    }

    /// <summary>Maps the content bytes of an encoded digest OID to its algorithm.</summary>
    /// <param name="oid">The OID's content bytes.</param>
    /// <param name="arc">The last arc of a NIST hash OID, or 0.</param>
    /// <returns>The algorithm.</returns>
    private static CmsDigestAlgorithm FromEncodedOid(ReadOnlySpan<byte> oid, out byte arc)
    {
        arc = 0;
        if (oid.SequenceEqual(Sha1Encoded))
        {
            return CmsDigestAlgorithm.Sha1;
        }

        if (oid.Length != NistHashPrefix.Length + 1 || !oid.StartsWith(NistHashPrefix))
        {
            return CmsDigestAlgorithm.Unknown;
        }

        arc = oid[^1];
        return arc switch
        {
            Sha256Arc => CmsDigestAlgorithm.Sha256,
            Sha384Arc => CmsDigestAlgorithm.Sha384,
            Sha512Arc => CmsDigestAlgorithm.Sha512,
            Sha3_256Arc => CmsDigestAlgorithm.Sha3_256,
            Sha3_384Arc => CmsDigestAlgorithm.Sha3_384,
            Sha3_512Arc => CmsDigestAlgorithm.Sha3_512,
            Shake256Arc or Shake256LenArc => CmsDigestAlgorithm.Shake256,
            Shake128Arc or Shake128LenArc => CmsDigestAlgorithm.Shake128,
            _ => CmsDigestAlgorithm.Unknown,
        };
    }

    /// <summary>Checks SHAKE256 parameters: none for id-shake256, and exactly 512 bits for id-shake256-len.</summary>
    /// <param name="arc">The OID's last arc.</param>
    /// <param name="parameters">The encoded parameters, empty when absent.</param>
    /// <returns><see langword="true"/> when PDF allows them.</returns>
    private static bool Shake256ParametersAllowed(byte arc, ReadOnlySpan<byte> parameters)
    {
        if (arc == Shake256Arc)
        {
            return parameters.IsEmpty;
        }

        try
        {
            return AsnDecoder.TryReadInt32(parameters, AsnEncodingRules.DER, out var bits, out var consumed)
                && consumed == parameters.Length
                && bits == Shake256Bits;
        }
        catch (AsnContentException)
        {
            return false;
        }
    }

    /// <summary>Gets the status of an algorithm the platform may or may not provide.</summary>
    /// <param name="supported">Whether the platform provides it.</param>
    /// <returns>The status.</returns>
    private static CmsDigestStatus Available(bool supported) => supported ? CmsDigestStatus.Available : CmsDigestStatus.NotAvailable;
}
