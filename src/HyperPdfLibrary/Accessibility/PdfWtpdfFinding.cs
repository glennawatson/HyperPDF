// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>One place where a file falls short of Well-Tagged PDF.</summary>
/// <param name="Profile">The lowest level the rule applies to: <see cref="PdfWtpdfProfile.Reuse"/> rules also bind the accessibility level.</param>
/// <param name="Clause">The WTPDF 1.0 clause, such as <c>8.2.5.12</c>.</param>
/// <param name="Message">What is wrong, in plain words.</param>
/// <param name="ElementNumber">The object number of the tag involved; 0 when the finding is about the whole file.</param>
[DebuggerDisplay("PdfWtpdfFinding: {Clause} {Message}")]
public sealed record PdfWtpdfFinding(PdfWtpdfProfile Profile, string Clause, string Message, int ElementNumber);
