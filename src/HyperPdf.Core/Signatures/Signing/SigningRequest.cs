// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>What a certificate signature records.</summary>
/// <param name="PageIndex">The page the signature field belongs to.</param>
/// <param name="Reason">Why the document is signed, for example "Approved"; may be empty.</param>
/// <param name="Location">Where it was signed; may be empty.</param>
/// <param name="Time">When it was signed.</param>
[DebuggerDisplay("SigningRequest: Page {PageIndex}: {Reason}")]
public readonly record struct SigningRequest(int PageIndex, string Reason, string Location, DateTimeOffset Time);
