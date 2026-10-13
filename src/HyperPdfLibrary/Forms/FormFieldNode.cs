// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>A field in the form's hierarchy, as it was when the index was built.</summary>
[DebuggerDisplay("FormFieldNode: {Name}")]
internal sealed class FormFieldNode
{
    /// <summary>Initializes a new instance of the <see cref="FormFieldNode"/> class.</summary>
    /// <param name="name">The fully qualified name.</param>
    /// <param name="id">The object that holds the field; not valid for a field written directly in an array.</param>
    /// <param name="dictionary">The field dictionary.</param>
    /// <param name="parent">The parent field, or <see langword="null"/> for a root field.</param>
    internal FormFieldNode(string name, PdfObjectId id, PdfDictionary dictionary, FormFieldNode? parent)
    {
        Name = name;
        Id = id;
        Dictionary = dictionary;
        Parent = parent;
    }

    /// <summary>Gets the fully qualified name.</summary>
    internal string Name { get; }

    /// <summary>Gets the object that holds the field.</summary>
    internal PdfObjectId Id { get; }

    /// <summary>Gets the field dictionary.</summary>
    internal PdfDictionary Dictionary { get; }

    /// <summary>Gets the parent field, or <see langword="null"/> for a root field.</summary>
    internal FormFieldNode? Parent { get; }

    /// <summary>Gets the fields below this one.</summary>
    internal List<FormFieldNode> Children { get; } = [];

    /// <summary>Gets the widgets of the field: its widget kids, or the field itself when it is a merged widget.</summary>
    internal List<FormControl> Widgets { get; } = [];

    /// <summary>Gets a value indicating whether the field shows widgets and so holds a value; other fields only group.</summary>
    internal bool IsTerminal => Widgets.Count > 0;

    /// <summary>Gets a value indicating whether the field is a widget as well (one merged dictionary).</summary>
    internal bool IsMerged => Dictionary.IsName(KnownName.Subtype, KnownName.Widget);
}
