// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Decides when a document's integrity check is worth a notice, and what it says.</summary>
public static class IntegrityWarnings
{
    /// <summary>
    /// Describes a document whose integrity protection does not hold, is missing, or could not be checked here. A document
    /// whose protection was not checked is never described as intact.
    /// </summary>
    /// <param name="integrity">The check, or <see langword="null"/> when the document has no integrity protection.</param>
    /// <returns>The message, or <see langword="null"/> when there is nothing to warn about.</returns>
    public static string? Describe(DocumentIntegrity? integrity) => integrity is { NeedsWarning: true } ? integrity.Summary : null;
}
