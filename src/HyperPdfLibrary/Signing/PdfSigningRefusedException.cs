// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// Thrown when a signature is refused before anything is written: a lock, a certification or a seed value forbids it, the
/// certificate does not fit, or the file cannot take it. Each reason is a plain sentence a person can act on.
/// </summary>
[DebuggerDisplay("PdfSigningRefusedException: {Message}")]
public sealed class PdfSigningRefusedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PdfSigningRefusedException"/> class.</summary>
    public PdfSigningRefusedException()
        : this([])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfSigningRefusedException"/> class.</summary>
    /// <param name="message">The reason.</param>
    public PdfSigningRefusedException(string message)
        : this([message])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfSigningRefusedException"/> class.</summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The cause.</param>
    public PdfSigningRefusedException(string message, Exception innerException)
        : base(message, innerException) => Reasons = [message];

    /// <summary>Initializes a new instance of the <see cref="PdfSigningRefusedException"/> class.</summary>
    /// <param name="reasons">Every reason the signature is refused.</param>
    public PdfSigningRefusedException(IReadOnlyList<string> reasons)
        : base(reasons.Count == 0 ? "The signature was refused." : string.Join(' ', reasons)) => Reasons = reasons;

    /// <summary>Gets every reason the signature is refused, each a plain sentence.</summary>
    public IReadOnlyList<string> Reasons { get; } = [];
}
