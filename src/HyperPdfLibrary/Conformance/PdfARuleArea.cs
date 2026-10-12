// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>One area of PDF/A rules: the rules it checks and the pass that checks them.</summary>
/// <param name="Name">The area's name, for diagnostics.</param>
/// <param name="Rules">The rules the area checks. A rule listed here is reported as checked for every level that applies it.</param>
/// <param name="Check">Records the failures of the area's rules.</param>
[DebuggerDisplay("PdfARuleArea: {Name}, {Rules.Length} rules")]
internal sealed record PdfARuleArea(string Name, PdfARule[] Rules, Action<PdfAValidationContext> Check);
