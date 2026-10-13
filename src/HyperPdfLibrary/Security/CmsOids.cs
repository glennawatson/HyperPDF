// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>The encoded contents of the CMS object identifiers the public-key handler reads.</summary>
internal static class CmsOids
{
    /// <summary>Gets the content type of CMS enveloped data, <c>id-envelopedData</c> in RFC 5652 6.1.</summary>
    internal static ReadOnlySpan<byte> EnvelopedData => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x07, 0x03];

    /// <summary>Gets the elliptic curve public key algorithm, <c>id-ecPublicKey</c> in RFC 5480 2.1.1.</summary>
    internal static ReadOnlySpan<byte> EcPublicKey => [0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01];

    /// <summary>Gets the prefix shared by the NIST AES algorithms, 2.16.840.1.101.3.4.1.</summary>
    internal static ReadOnlySpan<byte> AesPrefix => [0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x01];

    /// <summary>Gets <c>dhSinglePass-stdDH-sha1kdf-scheme</c>, 1.3.133.16.840.63.0.2 (RFC 5753 7.1.4).</summary>
    internal static ReadOnlySpan<byte> StandardDhSha1 => [0x2B, 0x81, 0x05, 0x10, 0x86, 0x48, 0x3F, 0x00, 0x02];

    /// <summary>Gets <c>dhSinglePass-cofactorDH-sha1kdf-scheme</c>, 1.3.133.16.840.63.0.3 (RFC 5753 7.1.4).</summary>
    internal static ReadOnlySpan<byte> CofactorDhSha1 => [0x2B, 0x81, 0x05, 0x10, 0x86, 0x48, 0x3F, 0x00, 0x03];

    /// <summary>Gets the prefix of the SEC 1 <c>dhSinglePass-stdDH-sha*kdf-scheme</c> arcs, 1.3.132.1.11 (RFC 5753 7.1.4).</summary>
    internal static ReadOnlySpan<byte> StandardDhPrefix => [0x2B, 0x81, 0x04, 0x01, 0x0B];

    /// <summary>Gets the prefix of the SEC 1 <c>dhSinglePass-cofactorDH-sha*kdf-scheme</c> arcs, 1.3.132.1.14 (RFC 5753 7.1.4).</summary>
    internal static ReadOnlySpan<byte> CofactorDhPrefix => [0x2B, 0x81, 0x04, 0x01, 0x0E];
}
