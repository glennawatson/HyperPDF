// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signing;

/// <summary>The validation data that belongs to one signature: what a verifier needs to check it long after its certificates expire or their issuers stop answering.</summary>
/// <param name="Contents">The signature's /Contents value as stored (the DER container followed by its zero padding); its SHA-1 hash is the key of the signature's VRI entry.</param>
/// <param name="Certificates">The DER certificates: the signer's chain and the timestamp authority's.</param>
/// <param name="OcspResponses">The DER OCSP responses (RFC 6960) for those certificates.</param>
/// <param name="Crls">The DER certificate revocation lists (RFC 5280) for those certificates.</param>
[DebuggerDisplay("PdfSignatureValidationData: {Certificates.Length} certificates, {OcspResponses.Length} OCSP, {Crls.Length} CRLs")]
public sealed record PdfSignatureValidationData(byte[] Contents, byte[][] Certificates, byte[][] OcspResponses, byte[][] Crls);
