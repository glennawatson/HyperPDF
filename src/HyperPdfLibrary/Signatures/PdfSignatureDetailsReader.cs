// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Reads each signature field's modification-detection data, in the order of /AcroForm /Fields.</summary>
internal static class PdfSignatureDetailsReader
{
    /// <summary>Reads the details of every signature field.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The details, one per entry of <see cref="PdfDocumentAttachments.GetSignatures"/>.</returns>
    internal static PdfSignatureDetails[] Read(PdfDocument document)
    {
        var signatures = PdfDocumentAttachments.GetSignatures(document);
        if (signatures.Count == 0)
        {
            return [];
        }

        var names = document.Objects.Names;
        var fields = document.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields);
        var certification = document.Catalog.GetDictionary(KnownName.Perms)?.GetRaw(KnownName.DocMDP) ?? default;
        var result = new PdfSignatureDetails[signatures.Count];
        var index = 0;
        for (var i = 0; fields is not null && i < fields.Count && index < result.Length; i++)
        {
            var field = fields.GetDictionary(i);
            if (field?.IsName(KnownName.FT, KnownName.Sig) != true)
            {
                continue;
            }

            result[index] = Read(signatures[index], field, certification, names);
            index++;
        }

        return result;
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
            value?.GetText(KnownName.ContactInfo) ?? string.Empty)
        {
            SeedDigest = PdfSignatureDictionaries.ReadSeedDigest(field.GetDictionary(KnownName.SV), names),
            ReferenceDigestMethods = references.DigestMethods,
        };
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
