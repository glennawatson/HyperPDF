// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Reading;

/// <summary>
/// Something on a page that assistive technology needs and the reading blocks leave out: an empty table cell, an artifact, a
/// form field with its state, or an annotation with its description (PDF/UA, ISO 14289-1).
/// </summary>
/// <param name="Kind">What the item is.</param>
/// <param name="Name">The name a reader speaks: the artifact's text, the field's label or the annotation's description.</param>
/// <param name="Bounds">The item's box on the page; empty when it draws nothing.</param>
[DebuggerDisplay("PageSemanticItem: {Kind} {Name}")]
public sealed record PageSemanticItem(PageSemanticKind Kind, string Name, PageRect Bounds)
{
    /// <summary>Gets the role in words, such as "check box" or "text field"; <see langword="null"/> when the kind says it.</summary>
    public string? Role { get; init; }

    /// <summary>Gets a longer description, such as a field's tooltip; <see langword="null"/> for none.</summary>
    public string? Description { get; init; }

    /// <summary>Gets a form field's value in words; <see langword="null"/> for none.</summary>
    public string? Value { get; init; }

    /// <summary>Gets a form field's state in words, such as "checked, required, read only"; <see langword="null"/> for none.</summary>
    public string? State { get; init; }

    /// <summary>Gets what the field's formatted text looks like in words, such as "bold, italic"; <see langword="null"/> when it is plain.</summary>
    public string? RichText { get; init; }

    /// <summary>Gets the language when it differs from the document's; <see langword="null"/> otherwise.</summary>
    public string? Language { get; init; }

    /// <summary>Gets the table row of an empty cell, from 1; zero for other items.</summary>
    public int Row { get; init; }

    /// <summary>Gets the table column of an empty cell, from 1; zero for other items.</summary>
    public int Column { get; init; }

    /// <summary>Gets a value indicating whether the item is in optional content that is hidden, so a reader leaves it out.</summary>
    public bool IsHidden { get; init; }
}
