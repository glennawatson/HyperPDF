// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.TestAssets;

/// <summary>A file whose signature placeholder has its byte range filled in, waiting for the signature.</summary>
/// <param name="File">The file, with the byte range written.</param>
/// <param name="SignedBytes">The bytes the byte range covers.</param>
/// <param name="ContentsStart">The position of the /Contents hex string's opening bracket.</param>
[DebuggerDisplay("PreparedSignature: {SignedBytes.Length} signed bytes")]
public sealed record PreparedSignature(byte[] File, byte[] SignedBytes, int ContentsStart);
