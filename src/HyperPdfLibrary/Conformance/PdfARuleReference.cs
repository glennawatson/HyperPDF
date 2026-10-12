// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>Where a rule sits in a level's standard.</summary>
/// <param name="Rule">The rule.</param>
/// <param name="Clause">The clause, such as 6.1.7.1.</param>
/// <param name="Test">The test number within the clause, as veraPDF numbers it.</param>
internal readonly record struct PdfARuleReference(PdfARule Rule, string Clause, string Test);
