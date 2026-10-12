// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>
/// Creates digitally signed PDFs: the generated test document plus an incremental update adding a signature field,
/// signed (detached CMS, adbe.pkcs7.detached) over the whole file except the signature's contents.
/// </summary>
public static class TestSignedPdf
{
    /// <summary>The signer's common name.</summary>
    public static readonly string SignerName = "HyperPDF Test Signer";

    /// <summary>The signing reason recorded in the signature.</summary>
    public static readonly string Reason = "Approved";

    /// <summary>The hex digits reserved for the signature.</summary>
    private const int ContentsHexLength = 16_384;

    /// <summary>The RSA key size of the test certificate.</summary>
    private const int KeyBits = 2048;

    /// <summary>The angle brackets around the signature's hex digits.</summary>
    private const int Brackets = 2;

    /// <summary>The OID of SHA-256.</summary>
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

    /// <summary>The width each byte range number is padded to.</summary>
    private const int ByteRangeWidth = 10;

    /// <summary>The placeholder written where the byte range goes.</summary>
    private static readonly string ByteRangePlaceholder = $"[0 {new string('0', ByteRangeWidth)} {new string('0', ByteRangeWidth)} {new string('0', ByteRangeWidth)}]";

    /// <summary>Creates a self-signed signing certificate valid from yesterday for a year.</summary>
    /// <param name="clock">The clock.</param>
    /// <returns>The certificate with its private key.</returns>
    public static X509Certificate2 CreateCertificate(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        using var key = RSA.Create(KeyBits);
        var request = new CertificateRequest($"CN={SignerName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
        var now = clock.GetUtcNow();
        return request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));
    }

    /// <summary>Creates a signed PDF.</summary>
    /// <param name="pageCount">The number of pages.</param>
    /// <param name="certificate">The signing certificate, with its private key.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] Create(int pageCount, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        var original = TestPdf.Create(pageCount);
        var update = BuildUpdate(original);
        var bytes = new byte[original.Length + update.Length];
        original.CopyTo(bytes, 0);
        update.CopyTo(bytes, original.Length);

        var text = Encoding.Latin1.GetString(bytes);
        var contentsStart = text.IndexOf("/Contents <", StringComparison.Ordinal) + "/Contents ".Length;
        var contentsEnd = contentsStart + ContentsHexLength + Brackets;
        var byteRange = $"[0 {Pad(contentsStart)} {Pad(contentsEnd)} {Pad(bytes.Length - contentsEnd)}]";
        var rangeAt = text.IndexOf(ByteRangePlaceholder, StringComparison.Ordinal);
        Encoding.ASCII.GetBytes(byteRange).CopyTo(bytes, rangeAt);

        var signed = new byte[contentsStart + (bytes.Length - contentsEnd)];
        bytes.AsSpan(0, contentsStart).CopyTo(signed);
        bytes.AsSpan(contentsEnd).CopyTo(signed.AsSpan(contentsStart));
        var cms = new SignedCms(new(signed), true);
        cms.ComputeSignature(new(SubjectIdentifierType.IssuerAndSerialNumber, certificate) { DigestAlgorithm = new(Sha256Oid) });
        var hex = Convert.ToHexString(cms.Encode()).PadRight(ContentsHexLength, '0');
        Encoding.ASCII.GetBytes(hex).CopyTo(bytes, contentsStart + 1);
        return bytes;
    }

    /// <summary>Builds the incremental update: a signature dictionary, its field, the page and catalog that reference it.</summary>
    /// <param name="original">The original file.</param>
    /// <returns>The update bytes.</returns>
    private static byte[] BuildUpdate(byte[] original)
    {
        // The generated file numbers its objects predictably: 1 catalog, 2 pages, 3 font, 4 outlines, 5.. pages.
        const int catalog = 1;
        const int pages = 2;
        const int firstPage = 5;
        var size = CountObjects(original) + 1;
        var signature = size;
        var field = size + 1;
        var text = Encoding.Latin1.GetString(original);
        var pageBody = $"{ExtractObject(text, firstPage).TrimEnd('>', ' ', '\n')} /Annots [{field} 0 R] >>";
        var catalogBody = $"<< /Type /Catalog /Pages {pages} 0 R /AcroForm << /Fields [{field} 0 R] /SigFlags 3 >> >>";
        var update = new StringBuilder();
        var offsets = new SortedDictionary<int, int>();
        void Write(int number, string body)
        {
            offsets[number] = original.Length + Encoding.Latin1.GetByteCount(update.ToString());
            _ = update.Append(CultureInfo.InvariantCulture, $"{number} 0 obj\n{body}\nendobj\n");
        }

        _ = update.Append('\n');
        Write(signature, $"""
            << /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /Name ({SignerName}) /Reason ({Reason}) /M (D:20260102030405Z)
               /ByteRange {ByteRangePlaceholder} /Contents <{new string('0', ContentsHexLength)}> >>
            """);
        Write(field, $"<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signature1) /Rect [0 0 0 0] /F 132 /P {firstPage} 0 R /V {signature} 0 R >>");
        Write(firstPage, pageBody);
        Write(catalog, catalogBody);
        var xref = original.Length + Encoding.Latin1.GetByteCount(update.ToString());
        _ = update.Append("xref\n");
        foreach (var (number, offset) in offsets)
        {
            _ = update.Append(CultureInfo.InvariantCulture, $"{number} 1\n{offset:D10} 00000 n \n");
        }

        var previous = text.LastIndexOf("startxref\n", StringComparison.Ordinal) + "startxref\n".Length;
        var previousXref = text[previous..text.IndexOf('\n', previous)];
        _ = update.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {field + 1} /Root {catalog} 0 R /Prev {previousXref} >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(update.ToString());
    }

    /// <summary>Pads a byte range number to the placeholder's width.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The padded number.</returns>
    private static string Pad(int value) => value.ToString(CultureInfo.InvariantCulture).PadLeft(ByteRangeWidth, '0');

    /// <summary>Counts the objects of a generated file from its trailer.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The number of objects.</returns>
    private static int CountObjects(byte[] file)
    {
        var text = Encoding.Latin1.GetString(file);
        var size = text.LastIndexOf("/Size ", StringComparison.Ordinal) + "/Size ".Length;
        var end = text.IndexOf(' ', size);
        return int.Parse(text.AsSpan(size, end - size), CultureInfo.InvariantCulture) - 1;
    }

    /// <summary>Gets an object's body from a generated file.</summary>
    /// <param name="text">The file as text.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The body.</returns>
    private static string ExtractObject(string text, int number)
    {
        var start = text.IndexOf($"\n{number} 0 obj\n", StringComparison.Ordinal) + $"\n{number} 0 obj\n".Length;
        return text[start..text.IndexOf("\nendobj", start, StringComparison.Ordinal)];
    }
}
