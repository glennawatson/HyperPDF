// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The parsed PDF/A rule table.</summary>
/// <param name="Descriptions">The description of each rule, indexed by <see cref="PdfARule"/>.</param>
/// <param name="ByLevel">The rules each level applies, indexed by <see cref="PdfALevel"/>.</param>
internal sealed record PdfARuleTable(string[] Descriptions, PdfARuleReference[][] ByLevel);
