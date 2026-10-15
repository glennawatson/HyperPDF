// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks the language identifiers of <c>/Lang</c> entries (ISO 14289-1 7.2; ISO 14289-2 8.4.4; ISO 32000-2 14.9.2).</summary>
internal static class AccessibilityLanguageTags
{
    /// <summary>
    /// Determines whether a <c>/Lang</c> value is a language identifier: subtags of one to eight characters joined by hyphens,
    /// letters only in the first and letters or digits after it. An empty value is not an identifier.
    /// </summary>
    /// <param name="tag">The value.</param>
    /// <returns><see langword="true"/> when it is well formed.</returns>
    internal static bool IsValid(ReadOnlySpan<char> tag) => !tag.IsEmpty && PdfALanguageTags.IsValid(tag, true);
}
