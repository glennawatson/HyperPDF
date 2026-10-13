// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using HyperPdf.Cms;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// The parts of a DER certificate that signing needs, found in place (RFC 5280, 4.1): the issuer name and serial number the signer
/// is identified by, the subject name, and the kind and size of the public key. Reading them takes no allocation, unlike
/// loading an <see cref="System.Security.Cryptography.X509Certificates.X509Certificate2"/>.
/// </summary>
/// <param name="Der">The certificate.</param>
/// <param name="Issuer">Where the issuer Name is, tag and length included.</param>
/// <param name="Serial">Where the contents of the serial number INTEGER are.</param>
/// <param name="Subject">Where the subject Name is, tag and length included.</param>
/// <param name="KeyKind">The kind of public key.</param>
/// <param name="KeyParameters">Where the key algorithm's parameters are, tag and length included, or an empty range when there are none.</param>
/// <param name="KeyBits">The size of an RSA modulus or an elliptic-curve field in bits, or 0 when it is not known.</param>
[DebuggerDisplay("PdfSignerCertificate: {Der.Length} bytes, {KeyKind} {KeyBits}")]
internal readonly record struct PdfSignerCertificate(byte[] Der, Range Issuer, Range Serial, Range Subject, PdfSignerKeyKind KeyKind, Range KeyParameters, int KeyBits)
{
    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The bits in the 32-bit integer a leading-zero count works on.</summary>
    private const int IntegerBits = 32;

    /// <summary>The tag of the optional version field of a certificate: <c>[0]</c>, constructed.</summary>
    private static readonly Asn1Tag VersionTag = new(TagClass.ContextSpecific, 0, true);

    /// <summary>Gets the issuer Name as encoded.</summary>
    internal ReadOnlySpan<byte> IssuerName => Der.AsSpan()[Issuer];

    /// <summary>Gets the contents of the serial number INTEGER, as the certificate encodes it.</summary>
    internal ReadOnlySpan<byte> SerialNumber => Der.AsSpan()[Serial];

    /// <summary>Gets the encoded parameters of the key algorithm, empty when there are none.</summary>
    internal ReadOnlySpan<byte> Parameters => Der.AsSpan()[KeyParameters];

    /// <summary>Gets a value indicating whether the key is an elliptic-curve key, whose signatures ISO/TS 32002 covers.</summary>
    internal bool IsEllipticCurve => KeyKind == PdfSignerKeyKind.EllipticCurve;

    /// <summary>Gets the encoded rsaEncryption OID (RFC 8017, A.1).</summary>
    private static ReadOnlySpan<byte> RsaEncoded => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01];

    /// <summary>Gets the encoded id-ecPublicKey OID (RFC 5480, 2.1.1).</summary>
    private static ReadOnlySpan<byte> EcPublicKeyEncoded => [0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01];

    /// <summary>Gets the encoded id-at-commonName OID (RFC 5280, A.1).</summary>
    private static ReadOnlySpan<byte> CommonNameEncoded => [0x55, 0x04, 0x03];

    /// <summary>Reads the parts of a certificate.</summary>
    /// <param name="der">The DER certificate.</param>
    /// <returns>The parts.</returns>
    /// <exception cref="CryptographicException">The bytes are not a certificate.</exception>
    internal static PdfSignerCertificate Read(byte[] der)
    {
        ArgumentNullException.ThrowIfNull(der);
        return TryRead(der, out var certificate) ? certificate : throw new CryptographicException("The signing certificate is not a valid DER certificate.");
    }

    /// <summary>Gets the subject's common name, the last one in the encoded name, as the platform's simple name takes it.</summary>
    /// <returns>The name, or <see langword="null"/> when the subject has no common name that is plain text.</returns>
    internal string? CommonName()
    {
        if (!CmsDer.TryRead(Der.AsSpan()[Subject], out _, out var names, out _))
        {
            return null;
        }

        // Only the last name is kept, so find where it is and decode just that one.
        var foundTag = default(Asn1Tag);
        var found = default(ReadOnlySpan<byte>);
        var at = 0;
        while (CmsDer.TryNext(names, ref at, out _, out _, out var set))
        {
            if (!TryFindCommonName(set, out var tag, out var value))
            {
                continue;
            }

            foundTag = tag;
            found = value;
        }

        return found.IsEmpty ? null : DecodeText(foundTag, found);
    }

    /// <summary>Tries to read the parts of a certificate.</summary>
    /// <param name="der">The DER certificate.</param>
    /// <param name="certificate">Receives the parts.</param>
    /// <returns><see langword="true"/> when the certificate was read.</returns>
    private static bool TryRead(byte[] der, out PdfSignerCertificate certificate)
    {
        certificate = default;
        var span = der.AsSpan();
        if (!TryReadAt(span, 0, span.Length, out var outer) || !TryReadAt(span, outer.ContentStart, outer.End, out var tbs))
        {
            return false;
        }

        var cursor = tbs.ContentStart;
        if (!TryNext(span, ref cursor, tbs.End, out var serial) || (serial.Tag == VersionTag && !TryNext(span, ref cursor, tbs.End, out serial)))
        {
            return false;
        }

        if (!TryReadNames(span, ref cursor, tbs.End, out var issuer, out var subject) || !TryNext(span, ref cursor, tbs.End, out var key))
        {
            return false;
        }

        if (!TryReadKey(span, key, out var kind, out var parameters, out var bits))
        {
            return false;
        }

        certificate = new(der, issuer.Start..issuer.End, serial.ContentStart..serial.End, subject.Start..subject.End, kind, parameters, bits);
        return true;
    }

    /// <summary>Reads the issuer and subject names, which the signature algorithm and the validity period separate.</summary>
    /// <param name="der">The certificate.</param>
    /// <param name="cursor">Where the signature algorithm is; moved past the subject.</param>
    /// <param name="limit">Where the TBSCertificate ends.</param>
    /// <param name="issuer">Receives the issuer Name.</param>
    /// <param name="subject">Receives the subject Name.</param>
    /// <returns><see langword="true"/> when both were read.</returns>
    private static bool TryReadNames(ReadOnlySpan<byte> der, scoped ref int cursor, int limit, out Element issuer, out Element subject)
    {
        issuer = default;
        subject = default;
        return TryNext(der, ref cursor, limit, out _)
            && TryNext(der, ref cursor, limit, out issuer)
            && TryNext(der, ref cursor, limit, out _)
            && TryNext(der, ref cursor, limit, out subject);
    }

    /// <summary>Reads the kind and size of the key in a SubjectPublicKeyInfo.</summary>
    /// <param name="der">The certificate.</param>
    /// <param name="info">The SubjectPublicKeyInfo.</param>
    /// <param name="kind">Receives the kind of key.</param>
    /// <param name="parameters">Receives where the algorithm parameters are, empty for none.</param>
    /// <param name="bits">Receives the size in bits, or 0 when it is not known.</param>
    /// <returns><see langword="true"/> when the structure is well formed.</returns>
    private static bool TryReadKey(ReadOnlySpan<byte> der, Element info, out PdfSignerKeyKind kind, out Range parameters, out int bits)
    {
        kind = PdfSignerKeyKind.Other;
        parameters = default;
        bits = 0;
        var cursor = info.ContentStart;
        if (!TryNext(der, ref cursor, info.End, out var algorithm) || !TryNext(der, ref cursor, info.End, out var bitString))
        {
            return false;
        }

        var inner = algorithm.ContentStart;
        if (!TryNext(der, ref inner, algorithm.End, out var oid))
        {
            return false;
        }

        if (TryNext(der, ref inner, algorithm.End, out var algorithmParameters))
        {
            parameters = algorithmParameters.Start..algorithmParameters.End;
        }

        var identifier = der[oid.ContentStart..oid.End];
        if (identifier.SequenceEqual(RsaEncoded))
        {
            kind = PdfSignerKeyKind.Rsa;
            bits = ModulusBits(der, bitString);
        }
        else if (identifier.SequenceEqual(EcPublicKeyEncoded))
        {
            kind = PdfSignerKeyKind.EllipticCurve;
            bits = CurveBits(der[parameters]);
        }

        return true;
    }

    /// <summary>Gets the size of an RSA modulus in bits.</summary>
    /// <param name="der">The certificate.</param>
    /// <param name="bitString">The BIT STRING that holds the RSAPublicKey.</param>
    /// <returns>The bits, or 0 when the key cannot be read.</returns>
    private static int ModulusBits(ReadOnlySpan<byte> der, Element bitString)
    {
        // The first byte of a BIT STRING counts its unused bits; the RSAPublicKey follows.
        var start = bitString.ContentStart + 1;
        if (start >= bitString.End || !TryReadAt(der, start, bitString.End, out var key) || !TryReadAt(der, key.ContentStart, key.End, out var modulus))
        {
            return 0;
        }

        var magnitude = der[modulus.ContentStart..modulus.End].TrimStart((byte)0);
        return magnitude.IsEmpty ? 0 : ((magnitude.Length - 1) * BitsPerByte) + (IntegerBits - BitOperations.LeadingZeroCount((uint)magnitude[0]));
    }

    /// <summary>Gets the size in bits of the field of a named curve.</summary>
    /// <param name="parameters">The encoded parameters of the key algorithm.</param>
    /// <returns>The bits, or 0 when the parameters do not name a curve this library lists.</returns>
    private static int CurveBits(ReadOnlySpan<byte> parameters) =>
        CmsDer.TryRead(parameters, out var tag, out var oid, out _) && tag == Asn1Tag.ObjectIdentifier ? CmsEcCurves.FieldBits(CmsEcCurves.FromEncoded(oid)) : 0;

    /// <summary>Reads the element at a position.</summary>
    /// <param name="der">The certificate.</param>
    /// <param name="at">Where the element starts.</param>
    /// <param name="limit">Where the enclosing element ends.</param>
    /// <param name="element">Receives the element.</param>
    /// <returns><see langword="true"/> when a whole element is there.</returns>
    private static bool TryReadAt(ReadOnlySpan<byte> der, int at, int limit, out Element element)
    {
        element = default;
        if (at >= limit || !AsnDecoder.TryReadEncodedValue(der[at..limit], AsnEncodingRules.BER, out var tag, out var contentOffset, out _, out var consumed))
        {
            return false;
        }

        element = new(tag, at, at + contentOffset, at + consumed);
        return true;
    }

    /// <summary>Reads the element at the cursor and moves the cursor past it.</summary>
    /// <param name="der">The certificate.</param>
    /// <param name="cursor">Where to read; moved past the element.</param>
    /// <param name="limit">Where the enclosing element ends.</param>
    /// <param name="element">Receives the element.</param>
    /// <returns><see langword="true"/> when a whole element was read.</returns>
    private static bool TryNext(ReadOnlySpan<byte> der, scoped ref int cursor, int limit, out Element element)
    {
        if (!TryReadAt(der, cursor, limit, out element))
        {
            return false;
        }

        cursor = element.End;
        return true;
    }

    /// <summary>Finds the common name of a relative distinguished name, when it has one that is plain text.</summary>
    /// <param name="set">The contents of the SET OF attributes.</param>
    /// <param name="tag">Receives the string's tag.</param>
    /// <param name="text">Receives the string's contents.</param>
    /// <returns><see langword="true"/> when a non-empty, readable common name was found.</returns>
    private static bool TryFindCommonName(ReadOnlySpan<byte> set, out Asn1Tag tag, out ReadOnlySpan<byte> text)
    {
        var at = 0;
        while (CmsDer.TryNext(set, ref at, out _, out _, out var attribute))
        {
            if (TryReadCommonName(attribute, out tag, out text))
            {
                return true;
            }
        }

        tag = default;
        text = default;
        return false;
    }

    /// <summary>Reads an attribute that is a common name with a non-empty, readable string.</summary>
    /// <param name="attribute">The contents of the attribute: its type then its value.</param>
    /// <param name="tag">Receives the string's tag.</param>
    /// <param name="text">Receives the string's contents.</param>
    /// <returns><see langword="true"/> when the attribute is such a common name.</returns>
    private static bool TryReadCommonName(ReadOnlySpan<byte> attribute, out Asn1Tag tag, out ReadOnlySpan<byte> text)
    {
        var inner = 0;
        tag = default;
        text = default;
        if (!CmsDer.TryNext(attribute, ref inner, out var oidTag, out _, out var oid) || oidTag != Asn1Tag.ObjectIdentifier || !oid.SequenceEqual(CommonNameEncoded))
        {
            return false;
        }

        if (!CmsDer.TryNext(attribute, ref inner, out var valueTag, out _, out var value) || value.IsEmpty || !IsReadableText(valueTag))
        {
            return false;
        }

        tag = valueTag;
        text = value;
        return true;
    }

    /// <summary>Checks whether a directory string type is one that <see cref="DecodeText"/> reads.</summary>
    /// <param name="tag">The string's tag.</param>
    /// <returns><see langword="true"/> for a readable string type.</returns>
    private static bool IsReadableText(Asn1Tag tag) =>
        tag == new Asn1Tag(UniversalTagNumber.UTF8String)
        || tag == new Asn1Tag(UniversalTagNumber.PrintableString)
        || tag == new Asn1Tag(UniversalTagNumber.IA5String)
        || tag == new Asn1Tag(UniversalTagNumber.BMPString);

    /// <summary>Decodes a directory string.</summary>
    /// <param name="tag">The string's tag.</param>
    /// <param name="value">The string's contents.</param>
    /// <returns>The text, or <see langword="null"/> for a string type that is not read here.</returns>
    private static string? DecodeText(Asn1Tag tag, ReadOnlySpan<byte> value)
    {
        if (tag == new Asn1Tag(UniversalTagNumber.UTF8String) || tag == new Asn1Tag(UniversalTagNumber.PrintableString) || tag == new Asn1Tag(UniversalTagNumber.IA5String))
        {
            return Encoding.UTF8.GetString(value);
        }

        return tag == new Asn1Tag(UniversalTagNumber.BMPString) ? Encoding.BigEndianUnicode.GetString(value) : null;
    }

    /// <summary>One encoded element: its tag and where it starts, where its contents start and where it ends.</summary>
    /// <param name="Tag">The tag.</param>
    /// <param name="Start">Where the element starts.</param>
    /// <param name="ContentStart">Where the contents start.</param>
    /// <param name="End">Where the element ends.</param>
    private readonly record struct Element(Asn1Tag Tag, int Start, int ContentStart, int End);
}
