// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.PdfUa;

/// <summary>One thing a save found wrong with a file that claims PDF/UA-1.</summary>
/// <param name="Requirement">What the problem is about.</param>
/// <param name="Clause">The clause of ISO 14289-1:2014 that sets the requirement, such as 7.18.3.</param>
/// <param name="Message">A sentence in plain words for the reader.</param>
/// <param name="PageIndex">The zero based page the problem is on, or -1 when it is about the whole document.</param>
/// <param name="Repaired">Whether the save fixed it, so the written file no longer has it.</param>
[DebuggerDisplay("PdfUaProblem: {Requirement} page {PageIndex}, repaired {Repaired}")]
public sealed record PdfUaProblem(PdfUaRequirement Requirement, string Clause, string Message, int PageIndex, bool Repaired);
