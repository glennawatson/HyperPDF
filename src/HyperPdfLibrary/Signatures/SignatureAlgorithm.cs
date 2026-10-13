// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Cms;

namespace HyperPdfLibrary.Signatures;

/// <summary>A signature algorithm OID of a certificate, CRL or OCSP response, and how to verify it.</summary>
/// <param name="Oid">The signature algorithm OID.</param>
/// <param name="Digest">The digest it signs with.</param>
/// <param name="IsEcdsa">Whether it is ECDSA rather than RSA PKCS #1 v1.5.</param>
[DebuggerDisplay("SignatureAlgorithm: {Oid} {Digest}")]
internal readonly record struct SignatureAlgorithm(string Oid, CmsDigestAlgorithm Digest, bool IsEcdsa);
