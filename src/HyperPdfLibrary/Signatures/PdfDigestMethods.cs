// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Cms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Maps /DigestMethod names (ISO 32000-2, Tables 237 and 256; ISO/TS 32001:2022, 5.1.2) and CMS digests to <see cref="PdfDigestMethod"/>.</summary>
internal static class PdfDigestMethods
{
    /// <summary>The names, indexed by <see cref="PdfDigestMethod"/> value; index 0 is unused.</summary>
    private static readonly byte[][] Names =
    [
        [],
        "MD5"u8.ToArray(),
        "SHA1"u8.ToArray(),
        "SHA256"u8.ToArray(),
        "SHA384"u8.ToArray(),
        "SHA512"u8.ToArray(),
        "RIPEMD160"u8.ToArray(),
        "SHA3-256"u8.ToArray(),
        "SHA3-384"u8.ToArray(),
        "SHA3-512"u8.ToArray(),
        "SHAKE256"u8.ToArray(),
    ];

    /// <summary>Maps a /DigestMethod name.</summary>
    /// <param name="name">The name's bytes.</param>
    /// <returns>The method, or <see cref="PdfDigestMethod.Unknown"/>.</returns>
    internal static PdfDigestMethod FromName(ReadOnlySpan<byte> name)
    {
        for (var i = 1; i < Names.Length; i++)
        {
            if (name.SequenceEqual(Names[i]))
            {
                return (PdfDigestMethod)i;
            }
        }

        return PdfDigestMethod.Unknown;
    }

    /// <summary>Maps a name object.</summary>
    /// <param name="name">The name.</param>
    /// <param name="names">Its name table.</param>
    /// <returns>The method, or <see cref="PdfDigestMethod.Unknown"/>.</returns>
    internal static PdfDigestMethod FromName(PdfName name, PdfNameTable names) => name.IsNone ? PdfDigestMethod.Unknown : FromName(names.GetSpelling(name));

    /// <summary>Maps a CMS digest OID; id-shake256-len maps to SHAKE256 whatever its length, which the CMS check judges.</summary>
    /// <param name="oid">The OID.</param>
    /// <returns>The method, or <see cref="PdfDigestMethod.Unknown"/>.</returns>
    internal static PdfDigestMethod FromOid(string? oid) => CmsDigests.FromOid(oid) switch
    {
        CmsDigestAlgorithm.Sha1 => PdfDigestMethod.Sha1,
        CmsDigestAlgorithm.Sha256 => PdfDigestMethod.Sha256,
        CmsDigestAlgorithm.Sha384 => PdfDigestMethod.Sha384,
        CmsDigestAlgorithm.Sha512 => PdfDigestMethod.Sha512,
        CmsDigestAlgorithm.Sha3_256 => PdfDigestMethod.Sha3_256,
        CmsDigestAlgorithm.Sha3_384 => PdfDigestMethod.Sha3_384,
        CmsDigestAlgorithm.Sha3_512 => PdfDigestMethod.Sha3_512,
        CmsDigestAlgorithm.Shake256 => PdfDigestMethod.Shake256,
        _ => PdfDigestMethod.Unknown,
    };
}
