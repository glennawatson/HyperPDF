// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Markup;

/// <summary>A named numeric choice offered by an annotation tool.</summary>
/// <param name="NameKey">The display name.</param>
/// <param name="Value">The numeric value.</param>
[DebuggerDisplay("{Name}: {Value}")]
public readonly record struct AnnotationValueOption(string NameKey, float Value)
{
    /// <summary>Gets the localized display text.</summary>
    public string Name => UiText.Get(NameKey);
}
