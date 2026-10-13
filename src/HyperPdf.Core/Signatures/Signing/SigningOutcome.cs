// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>A signed, timestamped or updated file and what the user should know about how it was made.</summary>
/// <param name="Bytes">The whole file: the original bytes followed by the incremental update.</param>
/// <param name="Notes">What the user should know, each a plain sentence; empty when there is nothing to say.</param>
[DebuggerDisplay("SigningOutcome: {Bytes.Length} bytes, {Notes.Count} notes")]
public sealed record SigningOutcome(byte[] Bytes, IReadOnlyList<string> Notes);
