// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>The result veraPDF recorded for one of veraPDF's PDF/UA test files.</summary>
/// <param name="Path">The path relative to the checkout, with <c>/</c> separators.</param>
/// <param name="Part">The part of ISO 14289 the file was validated against: 1 or 2.</param>
/// <param name="Completed">Whether veraPDF finished the validation normally.</param>
/// <param name="Failed">The failed rules as <c>clause:test</c>.</param>
internal sealed record VeraPdfUaRecordedResult(string Path, int Part, bool Completed, HashSet<string> Failed);
