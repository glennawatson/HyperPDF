// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>Something a PDF/A conversion changed, or a reason it stopped, with the clause of the standard behind it.</summary>
/// <param name="Specification">The standard, such as ISO 19005-2:2011.</param>
/// <param name="Clause">The clause, such as 6.6.1.</param>
/// <param name="Description">What happened, in plain words.</param>
[DebuggerDisplay("PdfAConversionNote: {Clause} {Description}")]
public sealed record PdfAConversionNote(string Specification, string Clause, string Description);
