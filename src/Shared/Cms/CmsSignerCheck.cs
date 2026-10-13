// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Cms;

/// <summary>The result of checking a CMS signer.</summary>
/// <param name="Digest">The signer's digest algorithm.</param>
/// <param name="DigestStatus">Whether the digest is allowed and available here.</param>
/// <param name="Signature">The signature result; <see cref="CmsSignatureStatus.Unsupported"/> when the digest is not available.</param>
[DebuggerDisplay("CmsSignerCheck: {Digest} {DigestStatus} {Signature}")]
internal readonly record struct CmsSignerCheck(CmsDigestAlgorithm Digest, CmsDigestStatus DigestStatus, CmsSignatureStatus Signature);
