// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>
/// Writes PDF 2.0 files with PDF MAC tokens, working only from ISO/TS 32004:2024 and the RFCs it cites: the extension
/// entry, /KDFSalt, the permission bit, a standalone token in the trailer or one attached to a signature, and the byte
/// ranges. No code from the library is used, and no external tool writes PDF MAC, so these files are the independent
/// check of the reader and the writers.
/// </summary>
internal static partial class MacPdf
{
    /// <summary>The page content.</summary>
    internal const string ContentText = "0 0 1 rg 20 20 100 100 re f";

    /// <summary>The title of the first revision.</summary>
    internal const string FirstTitle = "Quarterly report";

    /// <summary>The object number of the page content stream.</summary>
    internal const int ContentNumber = 4;

    /// <summary>The object number of the document information dictionary.</summary>
    internal const int InfoNumber = 5;

    /// <summary>The object number of the /Encrypt dictionary.</summary>
    internal const int EncryptNumber = 6;

    /// <summary>The object number of the signature dictionary.</summary>
    internal const int SignatureNumber = 9;

    /// <summary>The hexadecimal digits reserved for a signature's /Contents.</summary>
    internal const int SignatureDigits = 16_384;

    /// <summary>The marker that starts a signature's hexadecimal /Contents string.</summary>
    internal const string ContentsMarker = "/Contents <";

    /// <summary>The hexadecimal digits per byte.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>The angle brackets around a hexadecimal string.</summary>
    private const int Brackets = 2;

    /// <summary>The width each signature byte range number is padded to.</summary>
    private const int RangeWidth = 10;

    /// <summary>The number of tries the byte range numbers get to settle, as their digit counts change the file's length.</summary>
    private const int SettlePasses = 8;

    /// <summary>The seed of the trailer file identifier.</summary>
    private const byte IdSeed = 0x61;

    /// <summary>The length of the file identifier.</summary>
    private const int IdLength = 16;

    /// <summary>The object number the IV of the extension's revision string is taken from.</summary>
    private const int RevisionIvNumber = 20;

    /// <summary>The object number the IV of the extension's URL string is taken from.</summary>
    private const int UrlIvNumber = 21;

    /// <summary>The object number of the signature form field.</summary>
    private const int FieldNumber = 8;

    /// <summary>The object number of the interactive form dictionary.</summary>
    private const int FormNumber = 7;

    /// <summary>The object number of the catalog.</summary>
    private const int CatalogNumber = 1;

    /// <summary>The /Size of the first revision.</summary>
    private const int FirstSize = 7;

    /// <summary>The /Size after the signature is added.</summary>
    private const int SignedSize = 10;

    /// <summary>The first object of the signature revision's second table subsection.</summary>
    private const int SignedRunStart = 7;

    /// <summary>The number of objects in the signature revision's second table subsection.</summary>
    private const int SignedRunLength = 3;

    /// <summary>The object number of the page tree root.</summary>
    private const int PagesNumber = 2;

    /// <summary>The object number of the page.</summary>
    private const int PageNumber = 3;

    /// <summary>The last object number of the first revision.</summary>
    private const int LastNumber = 6;

    /// <summary>Builds the first revision: header, objects, table and a trailer with a standalone token.</summary>
    /// <param name="protection">How the file is encrypted.</param>
    /// <param name="digest">The token's digest name.</param>
    /// <param name="includeMac">Whether the trailer has an /AuthCode.</param>
    /// <returns>The file.</returns>
    internal static byte[] Create(MacProtection protection, string digest, bool includeMac)
    {
        var pdf = new PdfBytes();
        pdf.Raw("%PDF-2.0\n%âãÏÓ\n");
        pdf.Object(CatalogNumber, Catalog(protection, string.Empty));
        pdf.Object(PagesNumber, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        pdf.Object(PageNumber, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << >> /Contents 4 0 R >>");
        pdf.Stream(ContentNumber, protection.Protect(ContentNumber, Encoding.Latin1.GetBytes(ContentText)));
        pdf.Object(InfoNumber, Info(protection, FirstTitle));
        pdf.Object(EncryptNumber, protection.EncryptText);
        var head = pdf.ToArray();
        string Tail(string authCode) => string.Create(
            CultureInfo.InvariantCulture,
            $"xref\n0 {FirstSize}\n0000000000 65535 f \n{pdf.Entries(CatalogNumber, LastNumber)}"
            + $"trailer\n<< /Size {FirstSize} /Root 1 0 R /Info 5 0 R /Encrypt 6 0 R /ID [{Id()}] {authCode} >>\nstartxref\n{head.Length}\n%%EOF\n");
        return includeMac ? SealStandalone(head, Tail, protection, digest) : [.. head, .. Encoding.Latin1.GetBytes(Tail(string.Empty))];
    }

    /// <summary>Appends an incremental revision that replaces the information dictionary, with a new standalone token.</summary>
    /// <param name="file">The file so far.</param>
    /// <param name="protection">How the file is encrypted.</param>
    /// <param name="title">The new title.</param>
    /// <param name="refreshMac">Whether to write a new token; otherwise the previous /AuthCode is copied, as a program that does not know PDF MAC would.</param>
    /// <returns>The file.</returns>
    internal static byte[] AddUnsignedRevision(byte[] file, MacProtection protection, string title, bool refreshMac)
    {
        var previous = Encoding.Latin1.GetString(file);
        var pdf = new PdfBytes(file.Length);
        pdf.Object(InfoNumber, Info(protection, title));
        byte[] head = [.. file, .. pdf.ToArray()];
        var startPrevious = StartXref(previous);
        string Tail(string authCode) => string.Create(
            CultureInfo.InvariantCulture,
            $"xref\n0 1\n0000000000 65535 f \n{InfoNumber} 1\n{pdf.OffsetOf(InfoNumber):D10} 00000 n \n"
            + $"trailer\n<< /Size {FirstSize} /Root 1 0 R /Info 5 0 R /Encrypt 6 0 R /ID [{Id()}] /Prev {startPrevious} {authCode} >>\nstartxref\n{head.Length}\n%%EOF\n");
        return refreshMac
            ? SealStandalone(head, Tail, protection, MacToken.Sha256)
            : [.. head, .. Encoding.Latin1.GetBytes(Tail(StandaloneAuthCode().Match(previous).Value))];
    }

    /// <summary>Appends an incremental revision that adds a signature field and signs it, with the token attached to the signature.</summary>
    /// <param name="file">The file so far.</param>
    /// <param name="protection">How the file is encrypted.</param>
    /// <param name="signer">The signer's certificate with its private key.</param>
    /// <param name="attachMac">Whether to attach the token; otherwise the signature is plain and the trailer still says AttachedToSig.</param>
    /// <returns>The file.</returns>
    internal static byte[] AddSignedRevision(byte[] file, MacProtection protection, X509Certificate2 signer, bool attachMac)
    {
        var startPrevious = StartXref(Encoding.Latin1.GetString(file));
        var pdf = new PdfBytes(file.Length);
        var zeros = new string('0', RangeWidth);
        pdf.Object(CatalogNumber, Catalog(protection, "/AcroForm 7 0 R"));
        pdf.Object(FormNumber, "<< /Fields [8 0 R] /SigFlags 3 >>");
        var name = Convert.ToHexString(protection.Protect(FieldNumber, "Signature1"u8.ToArray()));
        pdf.Object(FieldNumber, $"<< /FT /Sig /T <{name}> /Type /Annot /Subtype /Widget /F 132 /Rect [0 0 0 0] /V 9 0 R >>");
        pdf.Object(
            SignatureNumber,
            $"<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 {zeros} {zeros} {zeros}] {ContentsMarker}{new string('0', SignatureDigits)}> >>");
        byte[] head = [.. file, .. pdf.ToArray()];
        var tail = string.Create(
            CultureInfo.InvariantCulture,
            $"xref\n0 1\n0000000000 65535 f \n{CatalogNumber} 1\n{pdf.OffsetOf(CatalogNumber):D10} 00000 n \n{SignedRunStart} {SignedRunLength}\n{pdf.Entries(FormNumber, SignatureNumber)}"
            + $"trailer\n<< /Size {SignedSize} /Root 1 0 R /Info 5 0 R /Encrypt 6 0 R /ID [{Id()}] /Prev {startPrevious} "
            + $"/AuthCode << /MACLocation /AttachedToSig /SigObjRef 9 0 R >> >>\nstartxref\n{head.Length}\n%%EOF\n");
        return Sign([.. head, .. Encoding.Latin1.GetBytes(tail)], protection, signer, attachMac);
    }

    /// <summary>Gets the byte range a standalone token covers, read from the last trailer.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The first length, the second start and the second length.</returns>
    internal static long[] StandaloneRange(byte[] file)
    {
        var match = StandaloneAuthCode().Matches(Encoding.Latin1.GetString(file))[^1];
        return [.. match.Groups["range"].Value.Split(' ').Select(static number => long.Parse(number, CultureInfo.InvariantCulture))];
    }

    /// <summary>Finds the offset of the last standalone token's first hexadecimal digit.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The offset of the first digit, after the opening bracket.</returns>
    internal static int LastMacOffset(byte[] file) => StandaloneAuthCode().Matches(Encoding.Latin1.GetString(file))[^1].Groups["mac"].Index;

    /// <summary>Gets the length of a standalone token's hexadecimal string, digits only.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The digit count.</returns>
    internal static int LastMacDigits(byte[] file) => StandaloneAuthCode().Matches(Encoding.Latin1.GetString(file))[^1].Groups["mac"].Length;

    /// <summary>Gets the position after the last byte a signature's byte range covers, which is the end of its revision.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The position.</returns>
    internal static int SignedEnd(byte[] file)
    {
        var match = SignatureRange().Match(Encoding.Latin1.GetString(file));
        return int.Parse(match.Groups["start"].Value, CultureInfo.InvariantCulture) + int.Parse(match.Groups["length"].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>Finds the first place some text occurs in a file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="text">The text.</param>
    /// <returns>The offset.</returns>
    /// <exception cref="InvalidOperationException">The text is not in the file.</exception>
    internal static int Find(byte[] file, string text)
    {
        var at = file.AsSpan().IndexOf(Encoding.Latin1.GetBytes(text));
        return at >= 0 ? at : throw new InvalidOperationException("The text is not in the file.");
    }

    /// <summary>Matches a standalone /AuthCode dictionary as the builder writes it.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"/AuthCode << /MAC <(?<mac>[0-9A-F]+)> /ByteRange \[0 (?<range>\d+ \d+ \d+)\] /MACLocation /Standalone >>")]
    private static partial Regex StandaloneAuthCode();

    /// <summary>Matches a signature's /ByteRange as the builder writes it, with numbers padded to ten digits.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"/ByteRange \[0 \d{10} (?<start>\d{10}) (?<length>\d{10})\]")]
    private static partial Regex SignatureRange();

    /// <summary>Gets the trailer file identifier.</summary>
    /// <returns>The two hexadecimal strings.</returns>
    private static string Id()
    {
        var hex = Convert.ToHexString(MacProtection.Sequence(IdLength, IdSeed));
        return $"<{hex}> <{hex}>";
    }

    /// <summary>Reads the last startxref offset.</summary>
    /// <param name="text">The file as Latin-1 text.</param>
    /// <returns>The offset.</returns>
    private static long StartXref(string text)
    {
        var at = text.LastIndexOf("startxref\n", StringComparison.Ordinal) + "startxref\n".Length;
        return long.Parse(text[at..text.IndexOf('\n', at)], CultureInfo.InvariantCulture);
    }

    /// <summary>Writes the catalog, with the ISO/TS 32004 extension (Table 1) and extra entries.</summary>
    /// <param name="protection">How the file is encrypted.</param>
    /// <param name="extra">Extra catalog entries.</param>
    /// <returns>The object text.</returns>
    private static string Catalog(MacProtection protection, string extra)
    {
        // The extension's two strings are encrypted like any string of the document (ISO/TS 32004 clause 4, NOTE).
        var revision = Convert.ToHexString(protection.Protect(RevisionIvNumber, ":2024"u8.ToArray()));
        var url = Convert.ToHexString(protection.Protect(UrlIvNumber, "https://www.iso.org/standard/45877.html"u8.ToArray()));
        return $"<< /Type /Catalog /Pages 2 0 R {extra} /Extensions << /ISO_ "
            + $"[<< /Type /DeveloperExtensions /BaseVersion /2.0 /ExtensionLevel 32004 /ExtensionRevision <{revision}> /URL <{url}> >>] >> >>";
    }

    /// <summary>Writes the information dictionary.</summary>
    /// <param name="protection">How the file is encrypted.</param>
    /// <param name="title">The title.</param>
    /// <returns>The object text.</returns>
    private static string Info(MacProtection protection, string title) =>
        $"<< /Title <{Convert.ToHexString(protection.Protect(InfoNumber, Encoding.Latin1.GetBytes(title)))}> >>";

    /// <summary>Writes the trailer tail with a standalone token, settling the byte range and then filling in the token.</summary>
    /// <param name="head">The bytes before the cross-reference section.</param>
    /// <param name="tail">Builds the cross-reference section and trailer around an /AuthCode entry.</param>
    /// <param name="protection">How the file is encrypted.</param>
    /// <param name="digest">The token's digest name.</param>
    /// <returns>The file.</returns>
    private static byte[] SealStandalone(byte[] head, Func<string, string> tail, MacProtection protection, string digest)
    {
        var digestLength = MacToken.Hash(digest, []).Length;
        var tokenLength = MacToken.Build(protection.FileKey, MacProtection.Salt, digest, new byte[digestLength], null).Length;
        var zeros = new string('0', tokenLength * DigitsPerByte);
        long first = 0;
        long start = 0;
        long second = 0;
        byte[] file = [];
        for (var pass = 0; pass < SettlePasses; pass++)
        {
            var authCode = string.Create(CultureInfo.InvariantCulture, $"/AuthCode << /MAC <{zeros}> /ByteRange [0 {first} {start} {second}] /MACLocation /Standalone >>");
            var tailBytes = Encoding.Latin1.GetBytes(tail(authCode));
            file = [.. head, .. tailBytes];
            var nextFirst = head.Length + tailBytes.AsSpan().IndexOf(Encoding.Latin1.GetBytes($"<{zeros}>"));
            var nextStart = nextFirst + zeros.Length + Brackets;
            var nextSecond = file.Length - nextStart;
            var settled = nextFirst == first && nextStart == start && nextSecond == second;
            first = nextFirst;
            start = nextStart;
            second = nextSecond;
            if (settled)
            {
                break;
            }
        }

        byte[] covered = [.. file.AsSpan(0, (int)first), .. file.AsSpan((int)start)];
        var token = MacToken.Build(protection.FileKey, MacProtection.Salt, digest, MacToken.Hash(digest, covered), null);
        Encoding.ASCII.GetBytes(Convert.ToHexString(token)).CopyTo(file, first + 1);
        return file;
    }

    /// <summary>Fills in a signature's byte range, signs the covered bytes, optionally attaches the token, and writes the container into /Contents.</summary>
    /// <param name="file">The file with a placeholder signature.</param>
    /// <param name="protection">How the file is encrypted.</param>
    /// <param name="signer">The signer.</param>
    /// <param name="attachMac">Whether to attach the token.</param>
    /// <returns>The file.</returns>
    private static byte[] Sign(byte[] file, MacProtection protection, X509Certificate2 signer, bool attachMac)
    {
        var text = Encoding.Latin1.GetString(file);
        var contentsStart = text.LastIndexOf(ContentsMarker, StringComparison.Ordinal) + ContentsMarker.Length - 1;
        var contentsEnd = contentsStart + SignatureDigits + Brackets;
        var range = string.Create(
            CultureInfo.InvariantCulture,
            $"[0 {contentsStart:D10} {contentsEnd:D10} {file.Length - contentsEnd:D10}]");
        Encoding.ASCII.GetBytes(range).CopyTo(file, text.LastIndexOf("[0 0000000000", StringComparison.Ordinal));
        byte[] covered = [.. file.AsSpan(0, contentsStart), .. file.AsSpan(contentsEnd)];
        var cms = new SignedCms(new(covered), true);
        cms.ComputeSignature(new(signer) { DigestAlgorithm = new("2.16.840.1.101.3.4.2.1"), IncludeOption = X509IncludeOption.EndCertOnly }, true);
        if (attachMac)
        {
            var token = MacToken.Build(
                protection.FileKey,
                MacProtection.Salt,
                MacToken.Sha256,
                MacToken.Hash(MacToken.Sha256, covered),
                MacToken.Hash(MacToken.Sha256, cms.SignerInfos[0].GetSignature()));
            AsnEncodedData attribute = new(new Oid(MacToken.MacDataOid), token);
            cms.SignerInfos[0].AddUnsignedAttribute(attribute);
        }

        Encoding.ASCII.GetBytes(Convert.ToHexString(cms.Encode())).CopyTo(file, contentsStart + 1);
        return file;
    }
}
