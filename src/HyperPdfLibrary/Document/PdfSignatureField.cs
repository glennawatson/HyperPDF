// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>A signature field's stored signature, before it is checked.</summary>
/// <param name="Index">The signature's position in the returned fields or validation results.</param>
/// <param name="Contents">The encoded signature, usually a CMS blob padded with zeros; empty when unsigned.</param>
/// <param name="ByteRange">Pairs of offset and length covering the signed bytes.</param>
/// <param name="SubFilter">The signature format, for example <c>adbe.pkcs7.detached</c>.</param>
/// <param name="Reason">The reason the signer gave, or an empty string.</param>
/// <param name="SigningTime">The signing time the file records, if any.</param>
[DebuggerDisplay("PdfSignatureField: {Index} {SubFilter}")]
public sealed record PdfSignatureField(int Index, byte[] Contents, long[] ByteRange, string SubFilter, string Reason, DateTimeOffset? SigningTime);
