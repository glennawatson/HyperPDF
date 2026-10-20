// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>The result of one PDF/X check.</summary>
/// <param name="Rule">A short stable name for the requirement, such as <c>OutputIntent</c>.</param>
/// <param name="Passed">Whether the file meets the requirement.</param>
/// <param name="Message">What was found, in plain words.</param>
[DebuggerDisplay("PdfXFinding: {Rule} passed {Passed}")]
public readonly record struct PdfXFinding(string Rule, bool Passed, string Message);
