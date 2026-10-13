// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Security;

/// <summary>The encoded contents of the object identifiers a PDF MAC token uses (ISO/TS 32004:2024 6 and RFC 5652).</summary>
internal static class PdfMacOids
{
    /// <summary>The dotted form of <c>id-ct-authData</c>, RFC 5652 9.1.</summary>
    internal const string AuthDataText = "1.2.840.113549.1.9.16.1.2";

    /// <summary>The dotted form of <c>id-ct-pdfMacIntegrityInfo</c>, ISO/TS 32004 6.2.</summary>
    internal const string IntegrityInfoText = "1.0.32004.1.0";

    /// <summary>The dotted form of <c>id-kdf-pdfMacWrapKdf</c>, ISO/TS 32004 6.4.</summary>
    internal const string WrapKdfText = "1.0.32004.1.1";

    /// <summary>The dotted form of <c>id-attr-pdfMacData</c>, ISO/TS 32004 6.5.2.</summary>
    internal const string MacDataText = "1.0.32004.1.2";

    /// <summary>The dotted form of AES-256 key wrap without padding, RFC 3394 and NIST CSOR.</summary>
    internal const string Aes256WrapText = "2.16.840.1.101.3.4.1.45";

    /// <summary>The dotted form of HMAC with SHA-256, RFC 4231.</summary>
    internal const string HmacSha256Text = "1.2.840.113549.2.9";

    /// <summary>The dotted form of <c>id-contentType</c>, RFC 5652 11.1.</summary>
    internal const string ContentTypeText = "1.2.840.113549.1.9.3";

    /// <summary>The dotted form of <c>id-messageDigest</c>, RFC 5652 11.2.</summary>
    internal const string MessageDigestText = "1.2.840.113549.1.9.4";

    /// <summary>The dotted form of <c>id-aa-CMSAlgorithmProtection</c>, RFC 8933 and RFC 6211.</summary>
    internal const string AlgorithmProtectionText = "1.2.840.113549.1.9.52";

    /// <summary>Gets the encoded <c>id-ct-authData</c>.</summary>
    internal static ReadOnlySpan<byte> AuthData => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x10, 0x01, 0x02];

    /// <summary>Gets the encoded <c>id-ct-pdfMacIntegrityInfo</c>.</summary>
    internal static ReadOnlySpan<byte> IntegrityInfo => [0x28, 0x81, 0xFA, 0x04, 0x01, 0x00];

    /// <summary>Gets the encoded <c>id-kdf-pdfMacWrapKdf</c>.</summary>
    internal static ReadOnlySpan<byte> WrapKdf => [0x28, 0x81, 0xFA, 0x04, 0x01, 0x01];

    /// <summary>Gets the encoded <c>id-attr-pdfMacData</c>.</summary>
    internal static ReadOnlySpan<byte> MacData => [0x28, 0x81, 0xFA, 0x04, 0x01, 0x02];

    /// <summary>Gets the encoded AES-256 key wrap without padding.</summary>
    internal static ReadOnlySpan<byte> Aes256Wrap => [0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x01, 0x2D];

    /// <summary>Gets the encoded HMAC with SHA-256.</summary>
    internal static ReadOnlySpan<byte> HmacSha256 => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x02, 0x09];

    /// <summary>Gets the encoded <c>id-contentType</c>.</summary>
    internal static ReadOnlySpan<byte> ContentType => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x03];

    /// <summary>Gets the encoded <c>id-messageDigest</c>.</summary>
    internal static ReadOnlySpan<byte> MessageDigest => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x04];

    /// <summary>Gets the encoded <c>id-aa-CMSAlgorithmProtection</c>.</summary>
    internal static ReadOnlySpan<byte> AlgorithmProtection => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x34];

    /// <summary>Determines whether an object identifier's contents are an expected identifier's. Identifiers are public, so the comparison need not be constant time.</summary>
    /// <param name="oid">The contents read from a token.</param>
    /// <param name="expected">The expected contents.</param>
    /// <returns><see langword="true"/> when they are the same.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Is(ReadOnlySpan<byte> oid, ReadOnlySpan<byte> expected) => oid.SequenceEqual(expected);
}
