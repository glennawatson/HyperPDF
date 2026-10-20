// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Security;

/// <summary>The outcome of protecting a document or removing its protection.</summary>
/// <param name="Refusal">Why nothing was written, in plain words, or <see langword="null"/> when the file was written.</param>
/// <param name="Notes">Things the person should know about the file that was written, in plain words.</param>
[DebuggerDisplay("PdfProtectionResult: written {Written}")]
public sealed record PdfProtectionResult(string? Refusal, IReadOnlyList<string> Notes)
{
    /// <summary>Gets a value indicating whether the file was written.</summary>
    public bool Written => Refusal is null;

    /// <summary>Returns the result when the file was written, and throws when it was refused.</summary>
    /// <returns>This result.</returns>
    /// <exception cref="PdfException">Nothing was written; <see cref="PdfException.Error"/> is <see cref="PdfError.Security"/> and the message holds <see cref="Refusal"/>.</exception>
    public PdfProtectionResult ThrowIfRefused() =>
        Refusal is null ? this : throw new PdfException(PdfError.Security, Refusal);
}
