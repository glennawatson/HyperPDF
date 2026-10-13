// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Cms;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>What a CMS signature container is made of.</summary>
/// <param name="Digest">The digest of the signed bytes and of the signed attributes.</param>
/// <param name="Algorithm">The kind of signature the key makes.</param>
/// <param name="Format">The signature format: <see cref="PdfSignatureFormat.Pkcs7Detached"/> or <see cref="PdfSignatureFormat.CadesDetached"/>.</param>
/// <param name="Certificates">The DER certificates to embed, the signing certificate first.</param>
/// <param name="SigningTime">The signing time; written as an attribute for adbe.pkcs7.detached only.</param>
[DebuggerDisplay("PdfCmsSpec: {Format} {Digest} {Algorithm}")]
internal readonly record struct PdfCmsSpec(CmsDigestAlgorithm Digest, PdfSignatureAlgorithm Algorithm, PdfSignatureFormat Format, byte[][] Certificates, DateTimeOffset SigningTime);
