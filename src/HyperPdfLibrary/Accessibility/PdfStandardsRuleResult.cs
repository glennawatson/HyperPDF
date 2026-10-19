// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>One rule of a profile and what HyperPdf found for a document.</summary>
/// <param name="Rule">The rule.</param>
/// <param name="Status">The result.</param>
[DebuggerDisplay("PdfStandardsRuleResult: {Rule.Clause}:{Rule.TestNumber} {Status}")]
public readonly record struct PdfStandardsRuleResult(PdfStandardsRule Rule, PdfStandardsRuleStatus Status);
