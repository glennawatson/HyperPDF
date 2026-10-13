// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>The dotted object identifiers the public-key encryption writer puts in CMS EnvelopedData.</summary>
internal static class CmsWriteOids
{
    /// <summary>Gets <c>id-envelopedData</c> (RFC 5652 6.1).</summary>
    internal const string EnvelopedData = "1.2.840.113549.1.7.3";

    /// <summary>Gets <c>id-data</c>, the content type of the encrypted content (RFC 5652 4).</summary>
    internal const string Data = "1.2.840.113549.1.7.1";

    /// <summary>Gets <c>rsaEncryption</c>, the key transport algorithm (RFC 3370 4.2.1).</summary>
    internal const string RsaEncryption = "1.2.840.113549.1.1.1";

    /// <summary>Gets <c>aes256-CBC</c>, the content cipher (RFC 3565 4.1).</summary>
    internal const string Aes256Cbc = "2.16.840.1.101.3.4.1.42";

    /// <summary>Gets <c>id-aes256-wrap</c>, the key wrap (RFC 3565 2.3.2).</summary>
    internal const string Aes256Wrap = "2.16.840.1.101.3.4.1.45";

    /// <summary>Gets <c>id-ecPublicKey</c>, the algorithm of the originator's ephemeral key (RFC 5753 3.1.1).</summary>
    internal const string EcPublicKey = "1.2.840.10045.2.1";

    /// <summary>Gets <c>dhSinglePass-stdDH-sha256kdf-scheme</c> (RFC 5753 7.1.4).</summary>
    internal const string StandardDhSha256 = "1.3.132.1.11.1";

    /// <summary>Gets <c>dhSinglePass-stdDH-sha384kdf-scheme</c> (RFC 5753 7.1.4).</summary>
    internal const string StandardDhSha384 = "1.3.132.1.11.2";

    /// <summary>Gets <c>dhSinglePass-stdDH-sha512kdf-scheme</c> (RFC 5753 7.1.4).</summary>
    internal const string StandardDhSha512 = "1.3.132.1.11.3";
}
