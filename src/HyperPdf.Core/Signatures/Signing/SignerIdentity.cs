// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>The public part of an external signer, such as a hardware token or a remote signing service.</summary>
/// <param name="Certificates">The DER-encoded certificates: the signing certificate first, then the chain that leads to a trusted root.</param>
/// <param name="KeyKind">The kind of signature the key makes.</param>
[DebuggerDisplay("SignerIdentity: {KeyKind} {Certificates.Count} certificates")]
public sealed record SignerIdentity(IReadOnlyList<byte[]> Certificates, SigningKeyKind KeyKind);
