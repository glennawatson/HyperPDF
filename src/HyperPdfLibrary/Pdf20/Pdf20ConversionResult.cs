// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Pdf20;

/// <summary>The outcome of saving a document as a PDF 2.0 copy.</summary>
/// <param name="Changes">What the copy changed, moved or left out to meet ISO 32000-2.</param>
/// <param name="Problems">Why the copy was refused; empty when it was saved.</param>
[DebuggerDisplay("Pdf20ConversionResult: converted {Converted}, {Changes.Length} changes, {Problems.Length} problems")]
public sealed record Pdf20ConversionResult(Pdf20ConversionNote[] Changes, Pdf20ConversionNote[] Problems)
{
    /// <summary>Gets a value indicating whether the copy was written.</summary>
    public bool Converted => Problems.Length == 0;
}
