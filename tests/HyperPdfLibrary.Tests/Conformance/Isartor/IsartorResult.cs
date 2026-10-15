// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Conformance.Isartor;

/// <summary>The result for one Isartor test file.</summary>
/// <param name="Path">The path relative to the suite folder, with <c>/</c> separators.</param>
/// <param name="Clause">The ISO 19005-1 clause the file was built to break, read from its name.</param>
/// <param name="Outcome">What the validator reported.</param>
/// <param name="FailedRules">The rules the validator failed, as <c>clause:test</c>, or the reason the file was not opened.</param>
internal sealed record IsartorResult(string Path, string Clause, IsartorOutcome Outcome, string[] FailedRules);
