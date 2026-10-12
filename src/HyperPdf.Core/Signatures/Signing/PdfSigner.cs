// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>
/// Signs a PDF with a certificate: appends an incremental update holding a signature field on the chosen page and a
/// detached CMS signature (adbe.pkcs7.detached, SHA-256) over the whole file except the signature itself. The original
/// bytes are untouched, so earlier signatures stay valid.
/// </summary>
public static class PdfSigner
{
    /// <summary>The hex digits reserved for the signature, room for a certificate chain.</summary>
    private const int ContentsHexLength = 32_768;

    /// <summary>The width each byte range number is padded to.</summary>
    private const int ByteRangeWidth = 10;

    /// <summary>The angle brackets around the signature's hex digits.</summary>
    private const int Brackets = 2;

    /// <summary>The OID of SHA-256.</summary>
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

    /// <summary>The unsigned attribute holding a signature's timestamp token.</summary>
    private const string TimestampTokenOid = "1.2.840.113549.1.9.16.2.14";

    /// <summary>Signature flags: the document has signatures and must only be appended to.</summary>
    private const int SigFlags = 3;

    /// <summary>Annotation flags: print and locked.</summary>
    private const int WidgetFlags = 132;

    /// <summary>The placeholder written where the byte range goes.</summary>
    private static readonly string ByteRangePlaceholder = $"[0 {new string('0', ByteRangeWidth)} {new string('0', ByteRangeWidth)} {new string('0', ByteRangeWidth)}]";

    /// <summary>Signs a PDF file into a new file.</summary>
    /// <param name="sourcePath">The PDF to sign.</param>
    /// <param name="destinationPath">Where to write the signed PDF; may be the same file.</param>
    /// <param name="certificate">The signing certificate, with its private key.</param>
    /// <param name="request">What the signature records.</param>
    public static void Sign(string sourcePath, string destinationPath, X509Certificate2 certificate, in SigningRequest request)
    {
        var signed = Sign(File.ReadAllBytes(sourcePath), certificate, request);
        var temporary = $"{destinationPath}.signing";
        File.WriteAllBytes(temporary, signed);
        File.Move(temporary, destinationPath, true);
    }

    /// <summary>Signs a PDF.</summary>
    /// <param name="source">The PDF's bytes.</param>
    /// <param name="certificate">The signing certificate, with its private key.</param>
    /// <param name="request">What the signature records.</param>
    /// <returns>The signed PDF.</returns>
    /// <exception cref="ArgumentException">The certificate has no private key.</exception>
    /// <exception cref="InvalidDataException">The PDF's structure could not be read.</exception>
    public static byte[] Sign(byte[] source, X509Certificate2 certificate, in SigningRequest request)
    {
        var (bytes, cms, contentsStart) = PrepareSignature(source, certificate, request);
        WriteContents(bytes, contentsStart, cms.Encode());
        return bytes;
    }

    /// <summary>Signs a PDF, adding a trusted timestamp to the signature when a timestamp authority is given.</summary>
    /// <param name="source">The PDF's bytes.</param>
    /// <param name="certificate">The signing certificate, with its private key.</param>
    /// <param name="request">What the signature records.</param>
    /// <param name="timestamper">The timestamp authority, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Stops waiting for the timestamp authority.</param>
    /// <returns>The signed PDF.</returns>
    /// <exception cref="ArgumentException">The certificate has no private key.</exception>
    /// <exception cref="InvalidDataException">The PDF's structure could not be read.</exception>
    /// <exception cref="CryptographicException">The signature is larger than the space reserved for it.</exception>
    public static async Task<byte[]> SignAsync(byte[] source, X509Certificate2 certificate, SigningRequest request, ISignatureTimestamper? timestamper, CancellationToken cancellationToken)
    {
        var (bytes, cms, contentsStart) = PrepareSignature(source, certificate, request);
        if (timestamper is not null)
        {
            // The token stamps the signature value and travels as an unsigned attribute of the signer (PAdES-T).
            var signerInfo = cms.SignerInfos[0];
            var token = await timestamper.TimestampAsync(signerInfo.GetSignature(), cancellationToken).ConfigureAwait(false);
            signerInfo.AddUnsignedAttribute(new(TimestampTokenOid, token));
        }

        WriteContents(bytes, contentsStart, cms.Encode());
        return bytes;
    }

    /// <summary>
    /// Adds a document timestamp (PAdES-LTA): a timestamp token from a timestamp authority over the whole file as it
    /// stands, so the document, its signatures and any long-term validation data can be shown to have existed then.
    /// </summary>
    /// <param name="source">The PDF's bytes, usually already signed.</param>
    /// <param name="timestamper">The timestamp authority.</param>
    /// <param name="cancellationToken">Stops waiting for the timestamp authority.</param>
    /// <returns>The timestamped PDF.</returns>
    /// <exception cref="InvalidDataException">The PDF's structure could not be read.</exception>
    /// <exception cref="CryptographicException">The token is larger than the space reserved for it.</exception>
    public static async Task<byte[]> AddDocumentTimestampAsync(byte[] source, ISignatureTimestamper timestamper, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timestamper);
        var structure = PdfReader.Read(source);
        var dictionary = $"<< /Type /DocTimeStamp /Filter /Adobe.PPKLite /SubFilter /ETSI.RFC3161 /ByteRange {ByteRangePlaceholder} /Contents <{new string('0', ContentsHexLength)}> >>";
        var update = BuildUpdate(structure, dictionary, 0);
        var bytes = new byte[source.Length + update.Length];
        source.CopyTo(bytes, 0);
        update.CopyTo(bytes, source.Length);
        var (signed, contentsStart) = PrepareRange(bytes, source.Length);
        WriteContents(bytes, contentsStart, await timestamper.TimestampAsync(signed, cancellationToken).ConfigureAwait(false));
        return bytes;
    }

    /// <summary>
    /// Adds long-term validation data (PAdES LTV): the certificates of the signers' chains and the revocation data
    /// that shows they were not revoked, stored in the document security store so the signatures can still be checked
    /// once the certificates expire or their issuers stop answering. An existing store is replaced by one holding both.
    /// </summary>
    /// <param name="source">The signed PDF's bytes.</param>
    /// <param name="certificates">The DER certificates to store.</param>
    /// <param name="ocspResponses">The DER OCSP responses to store.</param>
    /// <param name="crls">The DER certificate revocation lists to store.</param>
    /// <returns>The PDF with the store.</returns>
    /// <exception cref="InvalidDataException">The PDF's structure could not be read.</exception>
    public static byte[] AddValidationData(byte[] source, IReadOnlyList<byte[]> certificates, IReadOnlyList<byte[]> ocspResponses, IReadOnlyList<byte[]> crls)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(ocspResponses);
        ArgumentNullException.ThrowIfNull(crls);
        var structure = PdfReader.Read(source);
        var root = PdfPages.ReadReference(structure.Trailer, "Root"u8);
        var next = 0;
        foreach (var number in structure.Entries.Keys)
        {
            next = Math.Max(next, number + 1);
        }

        var objects = new SortedDictionary<int, string>();
        var store = new StringBuilder("<< /Type /DSS");
        foreach (var (key, items) in (ReadOnlySpan<(string, IReadOnlyList<byte[]>)>)[("Certs", certificates), ("OCSPs", ocspResponses), ("CRLs", crls)])
        {
            _ = store.Append(CultureInfo.InvariantCulture, $" /{key} [");
            foreach (var item in items)
            {
                objects[next] = string.Create(CultureInfo.InvariantCulture, $"<< /Length {item.Length} >>\nstream\n{Encoding.Latin1.GetString(item)}\nendstream");
                _ = store.Append(CultureInfo.InvariantCulture, $"{next} 0 R ");
                next++;
            }

            _ = store.Append(']');
        }

        var catalog = PdfReader.GetObject(structure, root).Span;
        objects[root] = PdfEditing.AddEntry(PdfEditing.RemoveKey(catalog, "DSS"u8), $"/DSS {store.Append(" >>")}");
        return [.. source, .. PdfUpdateWriter.Serialize(structure, objects, root, next)];
    }

    /// <summary>Builds the incremental update: signature, field, updated page and form, cross-reference and trailer.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="signatureDictionary">The signature dictionary, with placeholders for its byte range and contents.</param>
    /// <param name="pageIndex">The page the field belongs to.</param>
    /// <returns>The update's bytes.</returns>
    private static byte[] BuildUpdate(PdfStructure structure, string signatureDictionary, int pageIndex)
    {
        var trailer = structure.Trailer;
        var root = PdfPages.ReadReference(trailer, "Root"u8);
        _ = PdfSyntax.ReadLong(trailer, PdfSyntax.FindKey(trailer, 0, "Size"u8), out var declared);

        // A damaged file's /Size can be too small; new objects must not reuse a number already in the file.
        var size = Math.Max(declared, 1);
        foreach (var number in structure.Entries.Keys)
        {
            size = Math.Max(size, number + 1L);
        }

        var catalog = PdfReader.GetObject(structure, root);
        var page = PdfPages.FindPage(structure, catalog, pageIndex);
        var signature = (int)size;
        var field = signature + 1;
        var objects = new SortedDictionary<int, string>
        {
            [signature] = signatureDictionary,
            [field] = string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Annot /Subtype /Widget /FT /Sig /T {PdfEditing.HexText($"Signature {signature}")} /V {signature} 0 R /Rect [0 0 0 0] /F {WidgetFlags} /P {page} 0 R >>"),
            [page] = AddToArray(structure, PdfReader.GetObject(structure, page), "Annots"u8, field),
        };
        AddFieldToForm(structure, root, catalog, field, objects);
        return PdfUpdateWriter.Serialize(structure, objects, root, field + 1);
    }

    /// <summary>Adds the signature field to the document's form, creating the form when there is none.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="root">The catalog's object number.</param>
    /// <param name="catalog">The catalog.</param>
    /// <param name="field">The field's object number.</param>
    /// <param name="objects">The objects being written.</param>
    private static void AddFieldToForm(PdfStructure structure, int root, ReadOnlyMemory<byte> catalog, int field, SortedDictionary<int, string> objects)
    {
        var formAt = PdfSyntax.FindKey(catalog.Span, 0, "AcroForm"u8);
        if (formAt < 0)
        {
            objects[root] = PdfEditing.AddEntry(Encoding.Latin1.GetString(catalog.Span), string.Create(CultureInfo.InvariantCulture, $"/AcroForm << /Fields [{field} 0 R] /SigFlags {SigFlags} >>"));
            return;
        }

        var form = PdfReader.Resolve(structure, catalog, formAt);
        var withField = Encoding.Latin1.GetBytes(AddToArray(structure, form, "Fields"u8, field));
        var updated = PdfEditing.AddEntry(PdfEditing.RemoveKey(withField, "SigFlags"u8), string.Create(CultureInfo.InvariantCulture, $"/SigFlags {SigFlags}"));
        if (PdfSyntax.TryReadReference(catalog.Span, formAt, out var formNumber))
        {
            objects[formNumber] = updated;
            return;
        }

        objects[root] = PdfEditing.AddEntry(PdfEditing.RemoveKey(catalog.Span, "AcroForm"u8), $"/AcroForm {updated}");
    }

    /// <summary>Adds a reference to an array held under a key, resolving an indirect array into the dictionary.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The array's key.</param>
    /// <param name="number">The object to reference.</param>
    /// <returns>The updated dictionary text.</returns>
    private static string AddToArray(PdfStructure structure, ReadOnlyMemory<byte> dictionary, ReadOnlySpan<byte> key, int number)
    {
        var at = PdfSyntax.FindKey(dictionary.Span, 0, key);
        var existing = at < 0 ? default : PdfReader.Resolve(structure, dictionary, at);
        var array = PdfEditing.Append(existing.Span, string.Create(CultureInfo.InvariantCulture, $"{number} 0 R"));
        return PdfEditing.AddEntry(PdfEditing.RemoveKey(dictionary.Span, key), $"/{Encoding.ASCII.GetString(key)} {array}");
    }

    /// <summary>Writes the signature dictionary with room for the byte range and signature.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <param name="request">The request.</param>
    /// <returns>The dictionary text.</returns>
    private static string SignatureDictionary(X509Certificate2 certificate, in SigningRequest request)
    {
        var name = certificate.GetNameInfo(X509NameType.SimpleName, false);
        var builder = new StringBuilder("<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached");
        _ = builder.Append(CultureInfo.InvariantCulture, $" /Name {PdfEditing.HexText(name)} /M ({PdfDate(request.Time)})");
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            _ = builder.Append(CultureInfo.InvariantCulture, $" /Reason {PdfEditing.HexText(request.Reason)}");
        }

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            _ = builder.Append(CultureInfo.InvariantCulture, $" /Location {PdfEditing.HexText(request.Location)}");
        }

        _ = builder.Append(CultureInfo.InvariantCulture, $" /ByteRange {ByteRangePlaceholder} /Contents <{new string('0', ContentsHexLength)}> >>");
        return builder.ToString();
    }

    /// <summary>Formats a PDF date, for example <c>D:20260102030405+10'00'</c>.</summary>
    /// <param name="time">The time.</param>
    /// <returns>The date text.</returns>
    private static string PdfDate(DateTimeOffset time)
    {
        var offset = time.Offset;
        return string.Create(CultureInfo.InvariantCulture, $"D:{time:yyyyMMddHHmmss}{(offset < TimeSpan.Zero ? '-' : '+')}{Math.Abs(offset.Hours):D2}'{Math.Abs(offset.Minutes):D2}'");
    }

    /// <summary>
    /// Appends the signature's update to a copy of the PDF, fills in the byte range, and computes the CMS signature over
    /// everything except the signature's own digits. The caller writes the signature in, after any timestamp.
    /// </summary>
    /// <param name="source">The PDF's bytes.</param>
    /// <param name="certificate">The signing certificate, with its private key.</param>
    /// <param name="request">What the signature records.</param>
    /// <returns>The whole file with its update, the signature, and where the contents' opening bracket is.</returns>
    /// <exception cref="ArgumentException">The certificate has no private key.</exception>
    /// <exception cref="InvalidDataException">The PDF's structure could not be read.</exception>
    private static (byte[] Bytes, SignedCms Cms, int ContentsStart) PrepareSignature(byte[] source, X509Certificate2 certificate, in SigningRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(certificate);
        if (!certificate.HasPrivateKey)
        {
            throw new ArgumentException("The certificate has no private key, so it cannot sign.", nameof(certificate));
        }

        var structure = PdfReader.Read(source);
        var update = BuildUpdate(structure, SignatureDictionary(certificate, request), request.PageIndex);
        var bytes = new byte[source.Length + update.Length];
        source.CopyTo(bytes, 0);
        update.CopyTo(bytes, source.Length);
        var (signed, contentsStart) = PrepareRange(bytes, source.Length);
        var cms = new SignedCms(new(signed), true);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate) { DigestAlgorithm = new(Sha256Oid), IncludeOption = X509IncludeOption.WholeChain };
        _ = signer.SignedAttributes.Add(new Pkcs9SigningTime(request.Time.UtcDateTime));
        cms.ComputeSignature(signer, true);
        return (bytes, cms, contentsStart);
    }

    /// <summary>Fills in the byte range and returns the bytes it covers: everything except the signature's own digits.</summary>
    /// <param name="bytes">The whole file with its update.</param>
    /// <param name="updateStart">Where the update starts.</param>
    /// <returns>The signed bytes and where the contents' opening bracket is.</returns>
    private static (byte[] Signed, int ContentsStart) PrepareRange(byte[] bytes, int updateStart)
    {
        var update = bytes.AsSpan(updateStart);
        var contentsStart = updateStart + update.IndexOf("/Contents <"u8) + "/Contents "u8.Length;
        var contentsEnd = contentsStart + ContentsHexLength + Brackets;
        var rangeAt = updateStart + update.IndexOf(Encoding.ASCII.GetBytes(ByteRangePlaceholder));
        var byteRange = string.Create(CultureInfo.InvariantCulture, $"[0 {contentsStart:D10} {contentsEnd:D10} {bytes.Length - contentsEnd:D10}]");
        Encoding.ASCII.GetBytes(byteRange).CopyTo(bytes, rangeAt);

        var signed = new byte[contentsStart + (bytes.Length - contentsEnd)];
        bytes.AsSpan(0, contentsStart).CopyTo(signed);
        bytes.AsSpan(contentsEnd).CopyTo(signed.AsSpan(contentsStart));
        return (signed, contentsStart);
    }

    /// <summary>Writes the signature or token into the space reserved for it, as hex padded with zeros.</summary>
    /// <param name="bytes">The whole file.</param>
    /// <param name="contentsStart">Where the contents' opening bracket is.</param>
    /// <param name="der">The DER-encoded signature or token.</param>
    /// <exception cref="CryptographicException">It is larger than the space reserved for it.</exception>
    private static void WriteContents(byte[] bytes, int contentsStart, byte[] der)
    {
        var hex = Convert.ToHexString(der);
        if (hex.Length > ContentsHexLength)
        {
            throw new CryptographicException("The signature is larger than the space reserved for it.");
        }

        Encoding.ASCII.GetBytes(hex.PadRight(ContentsHexLength, '0')).CopyTo(bytes, contentsStart + 1);
    }
}
