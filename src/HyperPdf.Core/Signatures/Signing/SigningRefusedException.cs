// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>A lock, a certification, a seed value or the certificate forbids the signature.</summary>
[DebuggerDisplay("SigningRefusedException: {Message}")]
public sealed class SigningRefusedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SigningRefusedException"/> class.</summary>
    public SigningRefusedException()
        : this([])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SigningRefusedException"/> class.</summary>
    /// <param name="message">The reason.</param>
    public SigningRefusedException(string message)
        : this([message])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SigningRefusedException"/> class.</summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The cause.</param>
    public SigningRefusedException(string message, Exception innerException)
        : base(message, innerException) => Reasons = [message];

    /// <summary>Initializes a new instance of the <see cref="SigningRefusedException"/> class.</summary>
    /// <param name="reasons">Every reason the signature is refused.</param>
    public SigningRefusedException(IReadOnlyList<string> reasons)
        : base(reasons.Count == 0 ? "The signature was refused." : string.Join(' ', reasons)) => Reasons = reasons;

    /// <summary>Gets every reason the signature is refused, each a plain sentence.</summary>
    public IReadOnlyList<string> Reasons { get; } = [];
}
