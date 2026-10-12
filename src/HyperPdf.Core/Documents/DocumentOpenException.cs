// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>Thrown when a document fails to open.</summary>
[DebuggerDisplay("DocumentOpenException: {Error}: {Message}")]
public sealed class DocumentOpenException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DocumentOpenException"/> class.</summary>
    public DocumentOpenException()
        : this(DocumentOpenError.Unknown, "The document could not be opened.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DocumentOpenException"/> class.</summary>
    /// <param name="message">The message.</param>
    public DocumentOpenException(string message)
        : this(DocumentOpenError.Unknown, message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DocumentOpenException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DocumentOpenException(string message, Exception innerException)
        : base(message, innerException) => Error = DocumentOpenError.Unknown;

    /// <summary>Initializes a new instance of the <see cref="DocumentOpenException"/> class.</summary>
    /// <param name="error">The error category.</param>
    /// <param name="message">The message.</param>
    public DocumentOpenException(DocumentOpenError error, string message)
        : base(message) => Error = error;

    /// <summary>Gets the error category.</summary>
    public DocumentOpenError Error { get; }
}
