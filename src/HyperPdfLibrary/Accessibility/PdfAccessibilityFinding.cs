// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>One thing the accessibility report found.</summary>
/// <param name="Code">What the finding is about.</param>
/// <param name="Message">A short plain-English sentence for the reader.</param>
/// <param name="PageIndex">The zero based page the finding is on, or -1 when it is about the whole document.</param>
/// <param name="ElementId">The object id of the structure element, or <see langword="null"/> when there is none or it is written directly in its parent.</param>
[DebuggerDisplay("PdfAccessibilityFinding: {Code} page {PageIndex}")]
public readonly record struct PdfAccessibilityFinding(PdfAccessibilityCode Code, string Message, int PageIndex, PdfObjectId? ElementId);
