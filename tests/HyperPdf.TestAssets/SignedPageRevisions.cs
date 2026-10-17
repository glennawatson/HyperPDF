// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>Writes signed raw PDFs and incremental replacements of their signed page.</summary>
public static class SignedPageRevisions
{
    /// <summary>The signed page's object number.</summary>
    public static readonly int PageNumber = 3;

    /// <summary>A replacement that presents the signed page as an annotation.</summary>
    public static readonly string Annotation = "<< /Type /Annot /Subtype /Text /Rect [0 0 20 20] /Contents (shadow page) >>";

    /// <summary>A replacement that presents the signed page as metadata.</summary>
    public static readonly string Metadata = "<< /Type /Metadata /Subtype /XML /Length 0 >>\nstream\n\nendstream";

    /// <summary>A replacement that presents the signed page as an object stream.</summary>
    public static readonly string ObjectStream = "<< /Type /ObjStm /N 0 /First 0 /Length 0 >>\nstream\n\nendstream";

    /// <summary>An earlier page definition excluded by the signed cross-reference table.</summary>
    private const string HiddenPage = "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Rotate 90 >>\nendobj\n";

    /// <summary>The raw objects in the signed revision.</summary>
    private static readonly string[] Objects =
    [
        "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> /Perms << /DocMDP 5 0 R >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>",
        "<< /FT /Sig /T (Signature1) /V 5 0 R >>",
        PdfSigning.SignatureDictionary("adbe.pkcs7.detached", "/Reference [<< /Type /SigRef /TransformMethod /DocMDP /TransformParams << /Type /TransformParams /P 3 /V /1.2 >> >>]"),
    ];

    /// <summary>Writes and signs a revision, including any junk prefix in its offsets and signed bytes.</summary>
    /// <param name="certificate">The signing certificate.</param>
    /// <param name="prefix">Bytes before the PDF header, represented as Latin-1.</param>
    /// <returns>The signed revision.</returns>
    public static byte[] Signed(X509Certificate2 certificate, string prefix) => BuildSigned(certificate, prefix, string.Empty);

    /// <summary>Signs a revision whose cross-reference table hides an earlier duplicate page object.</summary>
    /// <param name="certificate">The signing certificate.</param>
    /// <returns>The signed bytes, with the canonical page selected by the signed table.</returns>
    public static byte[] SignedWithHiddenPage(X509Certificate2 certificate) => BuildSigned(certificate, string.Empty, HiddenPage);

    /// <summary>Appends only a cross-reference update that exposes the earlier hidden signed page definition.</summary>
    /// <param name="signed">The revision with a hidden page.</param>
    /// <returns>The PDF with an unsigned table selecting the hidden page.</returns>
    public static byte[] RepointPage(byte[] signed)
    {
        ArgumentNullException.ThrowIfNull(signed);
        var text = Encoding.Latin1.GetString(signed);
        var hidden = text.IndexOf(HiddenPage, StringComparison.Ordinal);
        var previousStart = text.LastIndexOf("startxref\n", StringComparison.Ordinal) + "startxref\n".Length;
        var previousEnd = text.IndexOf('\n', previousStart);
        var previous = int.Parse(text.AsSpan(previousStart, previousEnd - previousStart), CultureInfo.InvariantCulture);
        var update = string.Create(
            CultureInfo.InvariantCulture,
            $"xref\n3 1\n{hidden:D10} 00000 n \ntrailer\n<< /Size {Objects.Length + 1} /Root 1 0 R /Prev {previous} >>\nstartxref\n{signed.Length}\n%%EOF\n");
        return [.. signed, .. Encoding.Latin1.GetBytes(update)];
    }

    /// <summary>Appends a benign revision, then shadows the signed page after another EOF marker.</summary>
    /// <param name="signed">The signed revision.</param>
    /// <param name="replacement">The replacement page object's body.</param>
    /// <returns>The three-revision PDF.</returns>
    public static byte[] Shadow(byte[] signed, string replacement)
    {
        var intermediate = IncrementalPdf.Append(signed, new Dictionary<int, string> { [Objects.Length + 1] = "(unreferenced revision)" });
        byte[] extraMarker = [.. intermediate, .. "%%EOF\n"u8];
        return IncrementalPdf.Append(extraMarker, new Dictionary<int, string> { [PageNumber] = replacement });
    }

    /// <summary>Shadows a signed page while removing its certification or signature from the current catalog.</summary>
    /// <param name="signed">The signed revision.</param>
    /// <param name="replacementIndex">The catalog or field override to apply.</param>
    /// <returns>The unsigned update.</returns>
    public static byte[] RemoveSignatureAuthority(byte[] signed, int replacementIndex)
    {
        string[] catalogs =
        [
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>",
            "<< /Type /Catalog /Pages 2 0 R /Perms << /DocMDP 5 0 R >> >>",
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [] >> /Perms << /DocMDP 5 0 R >> >>",
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> /Perms << /DocMDP 5 0 R >> >>",
        ];
        var updates = new Dictionary<int, string> { [1] = catalogs[replacementIndex], [PageNumber] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Rotate 90 >>" };
        if (replacementIndex == catalogs.Length - 1)
        {
            updates[PageNumber + 1] = "<< /FT /Sig /T (Signature1) >>";
        }

        return IncrementalPdf.Append(signed, updates);
    }

    /// <summary>Builds a raw signed revision with optional bytes outside its selected objects.</summary>
    /// <param name="certificate">The signing certificate.</param>
    /// <param name="prefix">Bytes before the header.</param>
    /// <param name="hidden">An unselected duplicate definition.</param>
    /// <returns>The signed file.</returns>
    private static byte[] BuildSigned(X509Certificate2 certificate, string prefix, string hidden)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var output = new StringBuilder(prefix);
        _ = output.Append("%PDF-1.7\n");
        _ = output.Append(hidden);
        var offsets = new int[Objects.Length];
        for (var i = 0; i < Objects.Length; i++)
        {
            offsets[i] = output.Length;
            _ = output.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{Objects[i]}\nendobj\n");
        }

        var xref = output.Length;
        _ = output.Append(CultureInfo.InvariantCulture, $"xref\n0 {Objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            _ = output.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        _ = output.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {Objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return PdfSigning.SignDetached(Encoding.Latin1.GetBytes(output.ToString()), certificate);
    }
}
