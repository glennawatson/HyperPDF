// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>The result of checking an encrypted document's integrity protection, with a sentence in plain words.</summary>
/// <param name="Status">The result.</param>
/// <param name="Summary">One plain sentence that says what the result means for the reader.</param>
[DebuggerDisplay("DocumentIntegrity: {Status}")]
public sealed record DocumentIntegrity(DocumentIntegrityStatus Status, string Summary)
{
    /// <summary>Gets a value indicating whether the reader should be told, because the document is not known to be intact.</summary>
    public bool NeedsWarning => Status is DocumentIntegrityStatus.Invalid or DocumentIntegrityStatus.Missing or DocumentIntegrityStatus.Unverified;
}
