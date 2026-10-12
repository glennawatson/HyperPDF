// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The failures of one rule.</summary>
/// <param name="Count">How many objects break the rule.</param>
/// <param name="Context">Where the first failure was found, or <see langword="null"/>.</param>
internal readonly record struct PdfAFailure(int Count, string? Context);
