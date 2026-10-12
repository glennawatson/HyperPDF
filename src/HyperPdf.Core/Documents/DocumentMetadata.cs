// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>Descriptive information about a document.</summary>
[DebuggerDisplay("DocumentMetadata: {Title}")]
public sealed record DocumentMetadata
{
    /// <summary>Gets the title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the author.</summary>
    public string? Author { get; init; }

    /// <summary>Gets the subject.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the keywords.</summary>
    public string? Keywords { get; init; }

    /// <summary>Gets the creating application.</summary>
    public string? Creator { get; init; }

    /// <summary>Gets the producing application.</summary>
    public string? Producer { get; init; }

    /// <summary>Gets the creation date.</summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>Gets the modification date.</summary>
    public DateTimeOffset? Modified { get; init; }

    /// <summary>Gets the format version, for example "1.7".</summary>
    public string? FormatVersion { get; init; }

    /// <summary>Gets a value indicating whether the document is encrypted.</summary>
    public bool IsEncrypted { get; init; }

    /// <summary>Gets whether the source file has a valid linearization header.</summary>
    public bool IsLinearized { get; init; }

    /// <summary>Gets the encrypted document's file name when this is a wrapper PDF.</summary>
    public string? WrappedFileName { get; init; }

    /// <summary>Gets the cryptographic filter named by a wrapper PDF, or null for ordinary documents.</summary>
    public string? WrappedCryptographicFilter { get; init; }
}
