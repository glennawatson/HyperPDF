// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>The results of the rules that bear on one WCAG 2.2 success criterion.</summary>
/// <param name="Criterion">The success criterion number, such as <c>1.3.1</c>.</param>
/// <param name="Results">The results of the rules that bear on it, in table order.</param>
[DebuggerDisplay("PdfStandardsCriterionResult: {Criterion}, {Results.Length} rules")]
public sealed record PdfStandardsCriterionResult(string Criterion, PdfStandardsRuleResult[] Results);
