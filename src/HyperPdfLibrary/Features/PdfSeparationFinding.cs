// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>A problem found in one page's separation dictionary.</summary>
/// <param name="PageIndex">The zero based index of the page whose dictionary has the problem.</param>
/// <param name="Problem">What is wrong.</param>
[DebuggerDisplay("PdfSeparationFinding: {PageIndex} {Problem}")]
public readonly record struct PdfSeparationFinding(int PageIndex, PdfSeparationProblem Problem);
