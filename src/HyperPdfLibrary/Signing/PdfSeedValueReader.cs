// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>Reads a signature field's /SV seed value dictionary and its /Cert certificate seed value dictionary.</summary>
internal static class PdfSeedValueReader
{
    /// <summary>The most entries read from any array, so a damaged file cannot make signing allocate without bound.</summary>
    private const int MaxEntries = 256;

    /// <summary>The Adobe handler name.</summary>
    private const string AdobeHandler = "Adobe.PPKLite";

    /// <summary>Reads the seed values of a field.</summary>
    /// <param name="seed">The /SV dictionary, or <see langword="null"/>.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The seed values, or <see langword="null"/> when the field has none.</returns>
    internal static PdfSeedValues? Read(PdfDictionary? seed, PdfNameTable names)
    {
        if (seed is null)
        {
            return null;
        }

        var reasons = ReadTexts(seed.GetArray(names.Intern("Reasons"u8)));
        var timestamp = seed.GetDictionary(names.Intern("TimeStamp"u8));
        var mdp = seed.GetDictionary(names.Intern("MDP"u8));
        return new(
            seed.GetInt32(KnownName.Ff, 0),
            ReadName(seed, KnownName.Filter, names),
            ReadSubFilters(seed.GetArray(KnownName.SubFilter), names),
            ReadDigests(seed, names),
            seed.GetInt32(KnownName.V, 0),
            reasons,
            seed.ContainsKey(names.Intern("Reasons"u8)),
            mdp is not null && mdp.ContainsKey(KnownName.P) ? mdp.GetInt32(KnownName.P, 0) : -1,
            timestamp is null ? null : Ascii(timestamp.GetStringBytes(names.Intern("URL"u8))),
            timestamp?.GetInt32(KnownName.Ff, 0) == 1,
            seed.GetBoolean(names.Intern("AddRevInfo"u8)),
            ReadLockDocument(seed.GetName(names.Intern("LockDocument"u8)), names),
            seed.ContainsKey(names.Intern("AppearanceFilter"u8)) ? PdfText.Decode(seed.GetStringBytes(names.Intern("AppearanceFilter"u8))) : null,
            ReadCertificate(seed.GetDictionary(KnownName.Cert), names));
    }

    /// <summary>Determines whether a handler name is the one this library signs with.</summary>
    /// <param name="filter">The name.</param>
    /// <returns><see langword="true"/> for Adobe.PPKLite.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsSupportedHandler(string filter) => string.Equals(filter, AdobeHandler, StringComparison.Ordinal);

    /// <summary>Reads a name as text.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The name without the slash, or <see langword="null"/>.</returns>
    private static string? ReadName(PdfDictionary dictionary, PdfName key, PdfNameTable names)
    {
        var name = dictionary.GetName(key);
        return name.IsNone ? null : names.GetString(name);
    }

    /// <summary>Reads the acceptable sub-filters, mapping each name to the format it names.</summary>
    /// <param name="array">The /SubFilter array, or <see langword="null"/>.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The formats in order.</returns>
    private static PdfSignatureFormat[] ReadSubFilters(PdfArray? array, PdfNameTable names)
    {
        var count = Math.Min(array?.Count ?? 0, MaxEntries);
        var formats = new PdfSignatureFormat[count];
        for (var i = 0; i < count; i++)
        {
            formats[i] = names.GetString(array!.GetName(i)) switch
            {
                "adbe.pkcs7.detached" => PdfSignatureFormat.Pkcs7Detached,
                "ETSI.CAdES.detached" => PdfSignatureFormat.CadesDetached,
                _ => PdfSignatureFormat.Unknown,
            };
        }

        return formats;
    }

    /// <summary>Reads the acceptable digests.</summary>
    /// <param name="seed">The seed value dictionary.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The digests in order; empty when the entry is absent.</returns>
    private static PdfDigestMethod[] ReadDigests(PdfDictionary seed, PdfNameTable names) =>
        PdfSignatureDictionaries.ReadSeedDigest(seed, names)?.Methods ?? [];

    /// <summary>Reads an array of text strings.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <returns>The strings.</returns>
    private static string[] ReadTexts(PdfArray? array)
    {
        var count = Math.Min(array?.Count ?? 0, MaxEntries);
        var texts = new string[count];
        for (var i = 0; i < count; i++)
        {
            texts[i] = PdfText.Decode(array!.Get(i).AsStringBytes());
        }

        return texts;
    }

    /// <summary>Reads an array of byte strings.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <returns>The byte strings.</returns>
    private static byte[][] ReadBytes(PdfArray? array)
    {
        var count = Math.Min(array?.Count ?? 0, MaxEntries);
        var items = new byte[count][];
        for (var i = 0; i < count; i++)
        {
            items[i] = array!.Get(i).AsStringBytes().ToArray();
        }

        return items;
    }

    /// <summary>Reads the /LockDocument name.</summary>
    /// <param name="name">The name, or none.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The intent.</returns>
    private static PdfSeedLockDocument ReadLockDocument(PdfName name, PdfNameTable names) => name.IsNone
        ? PdfSeedLockDocument.Auto
        : names.GetString(name) switch
        {
            "true" => PdfSeedLockDocument.Lock,
            "false" => PdfSeedLockDocument.Unlock,
            _ => PdfSeedLockDocument.Auto,
        };

    /// <summary>Reads the ASCII text of a string.</summary>
    /// <param name="bytes">The string's bytes.</param>
    /// <returns>The text, or <see langword="null"/> when the string is empty.</returns>
    private static string? Ascii(ReadOnlySpan<byte> bytes) => bytes.IsEmpty ? null : Encoding.ASCII.GetString(bytes);

    /// <summary>Reads a certificate seed value dictionary.</summary>
    /// <param name="cert">The /Cert dictionary, or <see langword="null"/>.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The constraints, or <see langword="null"/>.</returns>
    private static PdfSeedCertificate? ReadCertificate(PdfDictionary? cert, PdfNameTable names)
    {
        if (cert is null)
        {
            return null;
        }

        var policies = ReadBytes(cert.GetArray(names.Intern("OID"u8)));
        var policyTexts = new string[policies.Length];
        for (var i = 0; i < policyTexts.Length; i++)
        {
            policyTexts[i] = Encoding.ASCII.GetString(policies[i]);
        }

        return new(
            cert.GetInt32(KnownName.Ff, 0),
            ReadBytes(cert.GetArray(names.Intern("Subject"u8))),
            ReadBytes(cert.GetArray(names.Intern("Issuer"u8))),
            policyTexts,
            ReadSubjectNames(cert.GetArray(names.Intern("SubjectDN"u8)), names),
            ReadAsciiTexts(cert.GetArray(names.Intern("KeyUsage"u8))));
    }

    /// <summary>Reads an array of ASCII strings.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <returns>The strings.</returns>
    private static string[] ReadAsciiTexts(PdfArray? array)
    {
        var bytes = ReadBytes(array);
        var texts = new string[bytes.Length];
        for (var i = 0; i < texts.Length; i++)
        {
            texts[i] = Encoding.ASCII.GetString(bytes[i]);
        }

        return texts;
    }

    /// <summary>Reads the /SubjectDN array: one dictionary per required set of name attributes.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The required attributes, one set per dictionary.</returns>
    private static PdfSeedNameAttribute[][] ReadSubjectNames(PdfArray? array, PdfNameTable names)
    {
        var count = Math.Min(array?.Count ?? 0, MaxEntries);
        var sets = new PdfSeedNameAttribute[count][];
        for (var i = 0; i < count; i++)
        {
            var dictionary = array!.GetDictionary(i);
            var attributes = new List<PdfSeedNameAttribute>(dictionary?.Count ?? 0);
            for (var at = 0; dictionary is not null && at < dictionary.Count && at < MaxEntries; at++)
            {
                var key = dictionary.GetKeyAt(at);
                attributes.Add(new(names.GetString(key), PdfText.Decode(dictionary.Get(key).AsStringBytes())));
            }

            sets[i] = [.. attributes];
        }

        return sets;
    }
}
