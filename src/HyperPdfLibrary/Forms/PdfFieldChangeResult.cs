// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>Whether a change to a field was made, or why it was refused.</summary>
/// <param name="Applied">Whether the change was made.</param>
/// <param name="Reason">A plain reason the change was refused; <see langword="null"/> when it was made.</param>
[DebuggerDisplay("PdfFieldChangeResult: {Applied} {Reason}")]
public sealed record PdfFieldChangeResult(bool Applied, string? Reason)
{
    /// <summary>Gets the result of a change that was made.</summary>
    public static PdfFieldChangeResult Done => new(true, null);

    /// <summary>Makes the result of a change that was refused.</summary>
    /// <param name="reason">The plain reason.</param>
    /// <returns>The result.</returns>
    public static PdfFieldChangeResult Refused(string reason) => new(false, reason);

    /// <summary>Returns the result when the change was made, and throws when it was refused.</summary>
    /// <returns>This result.</returns>
    /// <exception cref="InvalidOperationException">The change was refused; the message holds <see cref="Reason"/>.</exception>
    public PdfFieldChangeResult ThrowIfRefused() =>
        Applied ? this : throw new InvalidOperationException(Reason ?? "The field change was refused.");
}
