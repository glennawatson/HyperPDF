// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Signatures;

/// <summary>A digital signature as stored in the file, before it is checked.</summary>
/// <param name="Index">The signature's index in the document.</param>
/// <param name="Contents">The encoded signature (usually a CMS/PKCS #7 blob, possibly padded with zeros).</param>
/// <param name="ByteRange">Pairs of offset and length covering the signed bytes of the file.</param>
/// <param name="SubFilter">The signature format, for example <c>adbe.pkcs7.detached</c> or <c>ETSI.CAdES.detached</c>.</param>
/// <param name="Reason">The reason the signer gave, or an empty string.</param>
/// <param name="SigningTime">The signing time the file records, if any.</param>
[DebuggerDisplay("RawSignature: Signature {Index} ({SubFilter})")]
public sealed record RawSignature(int Index, byte[] Contents, long[] ByteRange, string SubFilter, string Reason, DateTimeOffset? SigningTime);
