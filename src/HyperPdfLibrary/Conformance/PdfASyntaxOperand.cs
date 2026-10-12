// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>An operand waiting for its operator, with the rules it breaks if the operator links it.</summary>
/// <param name="Kind">The kind of value.</param>
/// <param name="FirstRule">The index of its first rule in the operand findings.</param>
/// <param name="RuleCount">The number of rules it breaks.</param>
/// <param name="Start">Where its first token starts in the content, so it can be read again.</param>
internal readonly record struct PdfASyntaxOperand(PdfASyntaxValue Kind, int FirstRule, int RuleCount, int Start);
