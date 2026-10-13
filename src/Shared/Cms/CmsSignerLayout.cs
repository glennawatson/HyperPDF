// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Cms;

/// <summary>Where the parts of a CMS SignedData's first signer lie in the container's DER (RFC 5652, 5.1 and 5.3).</summary>
/// <param name="DigestAlgorithm">The signer's whole digestAlgorithm AlgorithmIdentifier.</param>
/// <param name="SignedAttributes">The whole <c>[0] IMPLICIT</c> signedAttrs value, empty when there are none.</param>
/// <param name="SignatureAlgorithm">The signer's whole signatureAlgorithm AlgorithmIdentifier.</param>
/// <param name="Signature">The signature value's bytes.</param>
/// <param name="Content">The encapsulated content's bytes, empty when the content is detached.</param>
[DebuggerDisplay("CmsSignerLayout: signature {Signature}")]
internal readonly record struct CmsSignerLayout(Range DigestAlgorithm, Range SignedAttributes, Range SignatureAlgorithm, Range Signature, Range Content);
