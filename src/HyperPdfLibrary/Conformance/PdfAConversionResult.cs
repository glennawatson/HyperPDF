// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>The outcome of saving a document as PDF/A.</summary>
/// <param name="Level">The level asked for.</param>
/// <param name="Changes">What the conversion changed to meet the level.</param>
/// <param name="Problems">What stopped the conversion; empty when it was saved.</param>
/// <param name="Report">The check of the converted file, or <see langword="null"/> when the conversion stopped before writing it.</param>
[DebuggerDisplay("PdfAConversionResult: {Level}, converted {Converted}, {Problems.Length} problems")]
public sealed record PdfAConversionResult(PdfALevel Level, PdfAConversionNote[] Changes, PdfAConversionNote[] Problems, PdfAValidationReport? Report)
{
    /// <summary>Gets a value indicating whether the file was converted and written.</summary>
    public bool Converted => Problems.Length == 0;
}
