// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Signatures;

/// <summary>A trusted timestamp (RFC 3161) on a signature or a document: proof from a timestamp authority of when it existed.</summary>
/// <param name="Time">The time the authority vouches for.</param>
/// <param name="Authority">The timestamp authority's name.</param>
/// <param name="IsValid">Whether the timestamp matches what it stamps and its own signature checks out.</param>
/// <param name="IsTrusted">Whether the authority's certificate chains to a certificate this computer trusts.</param>
[DebuggerDisplay("SignatureTimestamp: {Authority} at {Time}: valid {IsValid}, trusted {IsTrusted}")]
public sealed record SignatureTimestamp(DateTimeOffset Time, string Authority, bool IsValid, bool IsTrusted);
