// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Forms;

/// <summary>The parts of a field a change rewrites. Everything else in the field's dictionaries is kept as it was.</summary>
[Flags]
internal enum FormFieldParts
{
    /// <summary>No part of the field.</summary>
    None = 0,

    /// <summary>The tooltip (<c>/TU</c>).</summary>
    Tooltip = 1 << 0,

    /// <summary>The field flags (<c>/Ff</c>).</summary>
    Flags = 1 << 1,

    /// <summary>The value and everything stored with it (<c>/V</c>, <c>/I</c>, rich text and, for buttons, <c>/Opt</c>).</summary>
    Value = 1 << 2,

    /// <summary>The default value (<c>/DV</c>).</summary>
    Default = 1 << 3,

    /// <summary>The character limit (<c>/MaxLen</c>).</summary>
    MaxLength = 1 << 4,

    /// <summary>The text alignment (<c>/Q</c>).</summary>
    Quadding = 1 << 5,

    /// <summary>The default appearance string (<c>/DA</c>).</summary>
    Appearance = 1 << 6,

    /// <summary>The options of a choice field and the selection kept with them.</summary>
    Options = 1 << 7,

    /// <summary>The additional actions and the activation action.</summary>
    Actions = 1 << 8,

    /// <summary>The border colour (<c>/MK /BC</c>).</summary>
    BorderColor = 1 << 9,

    /// <summary>The background colour (<c>/MK /BG</c>).</summary>
    Background = 1 << 10,

    /// <summary>The border width and style (<c>/BS</c>).</summary>
    Border = 1 << 11,

    /// <summary>The caption or mark (<c>/MK /CA</c>).</summary>
    Caption = 1 << 12,
}
