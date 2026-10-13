// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Pdf20;

/// <summary>Something a PDF 2.0 copy changed, or a reason it stopped, with the clause of the standard behind it.</summary>
/// <param name="Specification">The standard, such as ISO 32000-2:2020.</param>
/// <param name="Clause">The clause, such as 14.3.3.</param>
/// <param name="Description">What happened, in plain words.</param>
[DebuggerDisplay("Pdf20ConversionNote: {Clause} {Description}")]
public sealed record Pdf20ConversionNote(string Specification, string Clause, string Description);
