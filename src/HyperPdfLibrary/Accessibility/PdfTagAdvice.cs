// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>A suggestion from the Tagged PDF Best Practice Guide: Syntax (PDF Association, version 1.0.1). It is advice, not a conformance rule, so a file with advice can still conform.</summary>
/// <param name="Clause">The guide's clause, such as <c>3.5</c>.</param>
/// <param name="Message">What to improve, in plain words.</param>
/// <param name="ElementNumber">The object number of the tag involved; 0 when the advice is about the whole file.</param>
[DebuggerDisplay("PdfTagAdvice: {Clause} {Message}")]
public sealed record PdfTagAdvice(string Clause, string Message, int ElementNumber);
