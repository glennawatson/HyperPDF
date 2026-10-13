// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Requirements;

/// <summary>The encryption settings an <c>Encryption</c> requirement asks for in its <c>/Encrypt</c> dictionary (ISO 32000-2 §12.11.1, Table 274).</summary>
/// <param name="Filter">The security handler name, such as <c>Standard</c>, or <see langword="null"/>.</param>
/// <param name="Version">The algorithm (<c>/V</c>), or 0.</param>
/// <param name="Revision">The standard handler revision (<c>/R</c>), or 0.</param>
[DebuggerDisplay("PdfRequirementEncryption: {Filter} V{Version} R{Revision}")]
public sealed record PdfRequirementEncryption(string? Filter, int Version, int Revision);
