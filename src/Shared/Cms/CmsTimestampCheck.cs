// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Cms;

/// <summary>The result of checking an RFC 3161 timestamp token against the data it stamps.</summary>
/// <param name="Digest">The message imprint's digest algorithm, or the token signer's when that is the one not available.</param>
/// <param name="DigestStatus">Whether that digest is allowed and available here.</param>
/// <param name="ImprintMatches">Whether the message imprint is the hash of the data.</param>
/// <param name="IsValid">Whether the imprint matches and the token's signature is good.</param>
[DebuggerDisplay("CmsTimestampCheck: {Digest} {DigestStatus} valid {IsValid}")]
internal readonly record struct CmsTimestampCheck(CmsDigestAlgorithm Digest, CmsDigestStatus DigestStatus, bool ImprintMatches, bool IsValid);
