// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Reads each signature field's modification-detection data, in the order of /AcroForm /Fields.</summary>
internal static class PdfSignatureDetailsReader
{
    /// <summary>Reads the details of every signature field.</summary>
    /// <param name="store">The document revision's objects.</param>
    /// <param name="result">Reusable scratch receiving the current revision's details.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Read(PdfObjectStore store, List<PdfSignatureDetails> result) => Read(store, result, []);

    /// <summary>Reads fields not already retained from a signed revision.</summary>
    /// <param name="store">The document revision's objects.</param>
    /// <param name="result">Reusable scratch receiving the current revision's details.</param>
    /// <param name="retained">Signed snapshots whose bytes and permissions are already retained.</param>
    internal static void Read(PdfObjectStore store, List<PdfSignatureDetails> result, ReadOnlySpan<PdfSignatureDetails> retained)
    {
        result.Clear();
        var fields = store.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields);
        if (fields is null || fields.Count == 0)
        {
            return;
        }

        var names = store.Names;
        var certification = store.Catalog.GetDictionary(KnownName.Perms)?.GetRaw(KnownName.DocMDP) ?? default;
        for (var i = 0; i < fields.Count; i++)
        {
            PdfOpenContext.ThrowIfCancelled(store.Context);
            var field = fields.GetDictionary(i);
            if (field?.IsName(KnownName.FT, KnownName.Sig) != true)
            {
                continue;
            }

            var value = field.GetDictionary(KnownName.V);
            if (IsRetained(value, retained))
            {
                continue;
            }

            var signature = PdfDocumentAttachments.ReadSignature(names, result.Count, value);
            result.Add(Read(signature, field, certification, names));
        }
    }

    /// <summary>Gets the format a /SubFilter names.</summary>
    /// <param name="subFilter">The sub-filter.</param>
    /// <returns>The format.</returns>
    internal static PdfSignatureFormat FormatOf(string subFilter) => subFilter switch
    {
        "adbe.pkcs7.detached" => PdfSignatureFormat.Pkcs7Detached,
        "ETSI.CAdES.detached" => PdfSignatureFormat.CadesDetached,
        "adbe.pkcs7.sha1" => PdfSignatureFormat.Pkcs7Sha1,
        "ETSI.RFC3161" => PdfSignatureFormat.Rfc3161,
        "adbe.x509.rsa_sha1" => PdfSignatureFormat.X509RsaSha1,
        _ => PdfSignatureFormat.Unknown,
    };

    /// <summary>Checks raw signature identity before copying bytes or decoding discarded metadata.</summary>
    /// <param name="value">The candidate signature dictionary.</param>
    /// <param name="retained">Previously retained signed snapshots.</param>
    /// <returns>Whether the same signature's bytes and ranges are already retained.</returns>
    private static bool IsRetained(PdfDictionary? value, ReadOnlySpan<PdfSignatureDetails> retained)
    {
        if (value?.GetArray(KnownName.ByteRange) is not { } range)
        {
            return false;
        }

        var contents = value.GetStringBytes(KnownName.Contents);
        foreach (var detail in retained)
        {
            var field = detail.Field;
            if (detail.IsSigned && range.Count == field.ByteRange.Length && contents.SequenceEqual(field.Contents)
                && HasSameRange(range, field.ByteRange))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Compares an unmaterialised signature byte range with a retained range.</summary>
    /// <param name="range">The raw range.</param>
    /// <param name="retained">The retained range of the same length.</param>
    /// <returns>Whether every offset and length matches.</returns>
    private static bool HasSameRange(PdfArray range, ReadOnlySpan<long> retained)
    {
        for (var index = 0; index < retained.Length; index++)
        {
            if (range.Get(index).AsInteger() != retained[index])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads one signature field.</summary>
    /// <param name="signature">The stored signature.</param>
    /// <param name="field">The field dictionary.</param>
    /// <param name="certification">The catalog's /Perms /DocMDP entry.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The details.</returns>
    private static PdfSignatureDetails Read(PdfSignatureField signature, PdfDictionary field, PdfValue certification, PdfNameTable names)
    {
        var value = field.GetDictionary(KnownName.V);
        var references = PdfSignatureDictionaries.ReadReferences(value?.GetArray(KnownName.Reference), names);
        return new(
            signature,
            PdfFieldNames.FullName(field),
            FormatOf(signature.SubFilter),
            IsCertification(field, value, certification),
            references.DocMdp,
            references.FieldMdp,
            PdfSignatureDictionaries.ReadLock(field.GetDictionary(KnownName.Lock), names),
            references.UsageRights,
            value?.GetText(KnownName.Name) ?? string.Empty,
            value?.GetText(KnownName.Location) ?? string.Empty,
            value?.GetText(KnownName.ContactInfo) ?? string.Empty,
            PdfSignatureDictionaries.ReadSeedDigest(field.GetDictionary(KnownName.SV), names),
            references.DigestMethods);
    }

    /// <summary>Determines whether the catalog's /Perms /DocMDP names a field's signature.</summary>
    /// <param name="field">The field.</param>
    /// <param name="value">The field's signature value, or <see langword="null"/>.</param>
    /// <param name="certification">The /DocMDP entry.</param>
    /// <returns><see langword="true"/> for the certification signature.</returns>
    private static bool IsCertification(PdfDictionary field, PdfDictionary? value, PdfValue certification)
    {
        if (value is null || certification.IsNull)
        {
            return false;
        }

        var own = field.GetRaw(KnownName.V);
        return certification.IsReference
            ? own.IsReference && own.AsReference().Number == certification.AsReference().Number
            : ReferenceEquals(certification.AsDictionary(), value);
    }
}
