// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Security;

/// <summary>Builds the <c>/Encrypt</c> dictionary of a new file (ISO 32000-2 Tables 20, 21, 23, 25 and 27).</summary>
internal static class PdfEncryptionDictionaries
{
    /// <summary>The <c>/V</c> of AES-128 crypt filters.</summary>
    internal const int Version4 = 4;

    /// <summary>The <c>/V</c> of AES-256.</summary>
    internal const int Version5 = 5;

    /// <summary>The <c>/V</c> of AES-GCM (ISO/TS 32003).</summary>
    internal const int Version6 = 6;

    /// <summary>The key length of AES-128 in bits.</summary>
    internal const int Aes128Bits = 128;

    /// <summary>The key length of AES-256 in bits.</summary>
    internal const int Aes256Bits = 256;

    /// <summary>The bits in a byte, to give the standard handler's crypt filter length in bytes.</summary>
    private const int ByteBits = 8;

    /// <summary>The entries every standard handler dictionary has: filter, version, revision, length, filters, the two filter names, owner and user strings, and permissions.</summary>
    private const int StandardEntries = 10;

    /// <summary>The entries revisions 6 and 7 add: the owner and user keys and the permissions check.</summary>
    private const int ModernEntries = 3;

    /// <summary>The entries of a crypt filter: type, method, authorisation event and length.</summary>
    private const int FilterEntries = 4;

    /// <summary>Builds the dictionary of the standard security handler.</summary>
    /// <param name="names">The document's name table.</param>
    /// <param name="algorithm">The cipher.</param>
    /// <param name="entries">The values worked out from the passwords.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <returns>The dictionary.</returns>
    internal static PdfDictionary Standard(PdfNameTable names, PdfEncryptionAlgorithm algorithm, StandardEncryptionEntries entries, bool encryptMetadata)
    {
        var bits = algorithm == PdfEncryptionAlgorithm.Aes128 ? Aes128Bits : Aes256Bits;
        var modern = entries.OwnerKey is not null && entries.UserKey is not null && entries.Perms is not null;
        var dictionary = new PdfDictionary(null, StandardEntries + (modern ? ModernEntries : 0) + (encryptMetadata ? 0 : 1));
        dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.Standard));
        dictionary.Set(KnownName.V, PdfValue.FromInteger(VersionOf(algorithm)));
        dictionary.Set(KnownName.R, PdfValue.FromInteger(entries.Revision));
        dictionary.Set(KnownName.Length, PdfValue.FromInteger(bits));
        SetDefaultFilter(dictionary, names.Intern("StdCF"u8), CreateFilter(names, algorithm, bits / ByteBits));
        dictionary.Set(KnownName.O, PdfValue.FromString(entries.Owner));
        dictionary.Set(KnownName.U, PdfValue.FromString(entries.User));
        if (modern)
        {
            dictionary.Set(KnownName.OE, PdfValue.FromString(entries.OwnerKey!));
            dictionary.Set(KnownName.UE, PdfValue.FromString(entries.UserKey!));
            dictionary.Set(KnownName.Perms, PdfValue.FromString(entries.Perms!));
        }

        dictionary.Set(KnownName.P, PdfValue.FromInteger(entries.Permissions));
        if (!encryptMetadata)
        {
            dictionary.Set(KnownName.EncryptMetadata, PdfValue.FromBoolean(false));
        }

        return dictionary;
    }

    /// <summary>
    /// Builds the dictionary of a public-key file: <c>/Filter /Adobe.PubSec</c> and <c>/SubFilter /adbe.pkcs7.s5</c> with one
    /// default crypt filter that holds the recipient lists (7.6.5.2, Table 27).
    /// </summary>
    /// <param name="names">The document's name table.</param>
    /// <param name="algorithm">The cipher: AES-128 or AES-256.</param>
    /// <param name="entries">The recipient lists.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <returns>The dictionary.</returns>
    internal static PdfDictionary PublicKey(PdfNameTable names, PdfEncryptionAlgorithm algorithm, PublicKeyEntries entries, bool encryptMetadata)
    {
        var bits = algorithm == PdfEncryptionAlgorithm.Aes128 ? Aes128Bits : Aes256Bits;
        var dictionary = new PdfDictionary(null);
        dictionary.Set(KnownName.Filter, PdfValue.FromName(names.Intern("Adobe.PubSec"u8)));
        dictionary.Set(KnownName.SubFilter, PdfValue.FromName(names.Intern("adbe.pkcs7.s5"u8)));
        dictionary.Set(KnownName.V, PdfValue.FromInteger(VersionOf(algorithm)));
        dictionary.Set(KnownName.Length, PdfValue.FromInteger(bits));

        // A public-key crypt filter states its length in bits, unlike the standard handler's, which uses bytes.
        var filter = CreateFilter(names, algorithm, bits, 1 + (encryptMetadata ? 0 : 1));
        var recipients = new PdfArray(null, entries.Recipients.Length);
        foreach (var list in entries.Recipients)
        {
            recipients.Add(PdfValue.FromString(list));
        }

        filter.Set(KnownName.Recipients, PdfValue.FromArray(recipients));
        if (!encryptMetadata)
        {
            filter.Set(KnownName.EncryptMetadata, PdfValue.FromBoolean(false));
        }

        SetDefaultFilter(dictionary, names.Intern("DefaultCryptFilter"u8), filter);
        return dictionary;
    }

    /// <summary>Gets the <c>/V</c> of a cipher.</summary>
    /// <param name="algorithm">The cipher.</param>
    /// <returns>4 for AES-128, 5 for AES-256 and 6 for AES-GCM.</returns>
    internal static int VersionOf(PdfEncryptionAlgorithm algorithm) => algorithm switch
    {
        PdfEncryptionAlgorithm.Aes128 => Version4,
        PdfEncryptionAlgorithm.Aes256 => Version5,
        _ => Version6,
    };

    /// <summary>Gets the crypt filter method name of a cipher.</summary>
    /// <param name="algorithm">The cipher.</param>
    /// <returns>AESV2, AESV3 or AESV4.</returns>
    internal static KnownName MethodOf(PdfEncryptionAlgorithm algorithm) => algorithm switch
    {
        PdfEncryptionAlgorithm.Aes128 => KnownName.AESV2,
        PdfEncryptionAlgorithm.Aes256 => KnownName.AESV3,
        _ => KnownName.AESV4,
    };

    /// <summary>Makes a crypt filter dictionary (Table 25): its type, method, authorisation event and key length.</summary>
    /// <param name="names">The document's name table.</param>
    /// <param name="algorithm">The cipher.</param>
    /// <param name="length">The filter's <c>/Length</c>: bytes for the standard handler, bits for a public-key handler.</param>
    /// <returns>The filter dictionary.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfDictionary CreateFilter(PdfNameTable names, PdfEncryptionAlgorithm algorithm, int length) => CreateFilter(names, algorithm, length, 0);

    /// <summary>Makes a crypt filter dictionary (Table 25) with room for entries the caller adds.</summary>
    /// <param name="names">The document's name table.</param>
    /// <param name="algorithm">The cipher.</param>
    /// <param name="length">The filter's <c>/Length</c>: bytes for the standard handler, bits for a public-key handler.</param>
    /// <param name="extra">The entries the caller is about to add.</param>
    /// <returns>The filter dictionary.</returns>
    internal static PdfDictionary CreateFilter(PdfNameTable names, PdfEncryptionAlgorithm algorithm, int length, int extra)
    {
        var entry = new PdfDictionary(null, FilterEntries + extra);
        entry.Set(KnownName.Type, PdfValue.FromName(names.Intern("CryptFilter"u8)));
        entry.Set(KnownName.CFM, PdfValue.FromName(MethodOf(algorithm)));
        entry.Set(KnownName.AuthEvent, PdfValue.FromName(names.Intern("DocOpen"u8)));
        entry.Set(KnownName.Length, PdfValue.FromInteger(length));
        return entry;
    }

    /// <summary>Adds <c>/CF</c>, <c>/StmF</c> and <c>/StrF</c> naming one crypt filter as the default for strings and streams.</summary>
    /// <param name="dictionary">The <c>/Encrypt</c> dictionary.</param>
    /// <param name="name">The filter's name in <c>/CF</c>.</param>
    /// <param name="filter">The filter dictionary.</param>
    internal static void SetDefaultFilter(PdfDictionary dictionary, PdfName name, PdfDictionary filter)
    {
        var filters = new PdfDictionary(null, 1);
        filters.Set(name, PdfValue.FromDictionary(filter));
        dictionary.Set(KnownName.CF, PdfValue.FromDictionary(filters));
        dictionary.Set(KnownName.StmF, PdfValue.FromName(name));
        dictionary.Set(KnownName.StrF, PdfValue.FromName(name));
    }
}
