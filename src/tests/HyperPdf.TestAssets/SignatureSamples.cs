// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdf.TestAssets;

/// <summary>
/// A small form with a text field and a signature field, signed in an incremental update, and later updates of each kind
/// a viewer or editor makes. Objects: 1 catalog, 2 pages, 3 page, 4 form, 5 text field "Name", 6 signature field
/// "Signature1", 7 signature value.
/// </summary>
public static class SignatureSamples
{
    /// <summary>The text field's object number.</summary>
    public static readonly int NameField = 5;

    /// <summary>The signature field's object number.</summary>
    public static readonly int SignatureField = 6;

    /// <summary>The signature value's object number.</summary>
    public static readonly int SignatureValue = 7;

    /// <summary>The object number of an added annotation.</summary>
    public static readonly int AddedAnnotation = 8;

    /// <summary>The object number of an added /DSS dictionary.</summary>
    public static readonly int SecurityStore = 11;

    /// <summary>The object number of the first added /DSS stream.</summary>
    private const int FirstStoreStream = 12;

    /// <summary>The catalog.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R >>";

    /// <summary>The page tree.</summary>
    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>The page.</summary>
    private const string Page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Annots [5 0 R 6 0 R] >>";

    /// <summary>The form.</summary>
    private const string Form = "<< /Fields [5 0 R 6 0 R] >>";

    /// <summary>The form after signing.</summary>
    private const string SignedForm = "<< /Fields [5 0 R 6 0 R] /SigFlags 3 >>";

    /// <summary>The text field.</summary>
    private const string TextField = "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /Rect [10 10 100 30] /P 3 0 R /V (Alice) >>";

    /// <summary>The unsigned signature field.</summary>
    private const string EmptySignatureField = "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signature1) /Rect [0 0 0 0] /F 132 /P 3 0 R >>";

    /// <summary>Builds the unsigned form.</summary>
    /// <returns>The file.</returns>
    public static byte[] Unsigned() => MiniPdf.Build(Catalog, Pages, Page, Form, TextField, EmptySignatureField);

    /// <summary>Signs the form's signature field in an incremental update.</summary>
    /// <param name="certificate">The signer.</param>
    /// <param name="docMdp">The DocMDP permission (1 to 3) for a certification signature, or 0 for an approval signature.</param>
    /// <param name="fieldLock">A /Lock dictionary for the field, or an empty string.</param>
    /// <returns>The signed file.</returns>
    public static byte[] Signed(X509Certificate2 certificate, int docMdp, string fieldLock)
    {
        ArgumentNullException.ThrowIfNull(fieldLock);
        var reference = docMdp > 0
            ? string.Create(CultureInfo.InvariantCulture, $"/Reference [<< /Type /SigRef /TransformMethod /DocMDP /TransformParams << /Type /TransformParams /P {docMdp} /V /1.2 >> >>]")
            : string.Empty;
        var catalog = docMdp > 0 ? "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R /Perms << /DocMDP 7 0 R >> >>" : Catalog;
        var lockEntry = fieldLock.Length > 0 ? $"/Lock {fieldLock}" : string.Empty;
        var update = new Dictionary<int, string>
        {
            [1] = catalog,
            [4] = SignedForm,
            [SignatureField] = $"<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signature1) /Rect [0 0 0 0] /F 132 /P 3 0 R {lockEntry} /V 7 0 R >>",
            [SignatureValue] = PdfSigning.SignatureDictionary("adbe.pkcs7.detached", reference),
        };
        return PdfSigning.SignDetached(IncrementalPdf.Append(Unsigned(), update), certificate);
    }

    /// <summary>Appends an update that adds a comment to the page.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The updated file.</returns>
    public static byte[] AddAnnotation(byte[] file) => IncrementalPdf.Append(file, new Dictionary<int, string>
    {
        [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Annots [5 0 R 6 0 R 8 0 R] >>",
        [AddedAnnotation] = "<< /Type /Annot /Subtype /Text /Rect [50 50 70 70] /Contents (A note) /P 3 0 R >>",
    });

    /// <summary>Appends an update that changes the text field's value.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The updated file.</returns>
    public static byte[] FillForm(byte[] file) => IncrementalPdf.Append(file, new Dictionary<int, string>
    {
        [NameField] = "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /Rect [10 10 100 30] /P 3 0 R /V (Bob) >>",
    });

    /// <summary>Appends an update that rotates the page.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The updated file.</returns>
    public static byte[] ChangePage(byte[] file) => IncrementalPdf.Append(file, new Dictionary<int, string>
    {
        [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Rotate 90 /Annots [5 0 R 6 0 R] >>",
    });

    /// <summary>Appends an update that changes how the document opens, which no permission covers.</summary>
    /// <param name="file">The file.</param>
    /// <param name="docMdp">The DocMDP permission the file was signed with, or 0.</param>
    /// <returns>The updated file.</returns>
    public static byte[] ChangeCatalog(byte[] file, int docMdp)
    {
        var catalog = SignedCatalog(docMdp);
        return IncrementalPdf.Append(file, new Dictionary<int, string> { [1] = string.Concat(catalog.AsSpan(0, catalog.Length - ">>".Length), " /PageMode /UseOutlines >>") });
    }

    /// <summary>Appends an update that adds a /DSS store with certificates, OCSP responses and CRLs.</summary>
    /// <param name="file">The file.</param>
    /// <param name="catalog">The catalog body as signed, ending in <c>&gt;&gt;</c>.</param>
    /// <param name="streams">The DER items: certificates, then OCSP responses, then CRLs.</param>
    /// <param name="counts">How many of the items are certificates, and how many are OCSP responses.</param>
    /// <param name="vri">A /VRI dictionary body, or an empty string.</param>
    /// <returns>The updated file.</returns>
    public static byte[] AddSecurityStore(byte[] file, string catalog, IReadOnlyList<byte[]> streams, StoreCounts counts, string vri)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentNullException.ThrowIfNull(counts);
        var update = new Dictionary<int, string>();
        var references = new string[streams.Count];
        for (var i = 0; i < streams.Count; i++)
        {
            update[FirstStoreStream + i] = MiniPdf.Stream(string.Empty, System.Text.Encoding.Latin1.GetString(streams[i]));
            references[i] = string.Create(CultureInfo.InvariantCulture, $"{FirstStoreStream + i} 0 R");
        }

        var certs = string.Join(' ', references[..counts.Certificates]);
        var ocsps = string.Join(' ', references[counts.Certificates..(counts.Certificates + counts.OcspResponses)]);
        var crls = string.Join(' ', references[(counts.Certificates + counts.OcspResponses)..]);
        var vriEntry = vri.Length > 0 ? $"/VRI {vri}" : string.Empty;
        update[SecurityStore] = $"<< /Certs [{certs}] /OCSPs [{ocsps}] /CRLs [{crls}] {vriEntry} >>";
        update[1] = string.Concat(catalog.AsSpan(0, catalog.Length - ">>".Length), " /DSS 11 0 R >>");
        return IncrementalPdf.Append(file, update);
    }

    /// <summary>Gets the catalog body as <see cref="Signed"/> writes it.</summary>
    /// <param name="docMdp">The DocMDP permission, or 0.</param>
    /// <returns>The catalog.</returns>
    public static string SignedCatalog(int docMdp) =>
        docMdp > 0 ? "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R /Perms << /DocMDP 7 0 R >> >>" : Catalog;
}
