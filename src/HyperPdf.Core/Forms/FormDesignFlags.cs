// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms;

/// <summary>The field flags a form design sets (ISO 32000-2 12.7.4, Tables 226, 228, 230 and 231). The kind of a field is not a flag here; <see cref="FormFieldKind"/> carries it.</summary>
[Flags]
public enum FormDesignFlags
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>The user cannot change the value.</summary>
    ReadOnly = 1 << 0,

    /// <summary>The field must have a value before the form is submitted.</summary>
    Required = 1 << 1,

    /// <summary>The value is left out when the form is submitted.</summary>
    NoExport = 1 << 2,

    /// <summary>A text field takes several lines.</summary>
    Multiline = 1 << 12,

    /// <summary>A text field hides what is typed.</summary>
    Password = 1 << 13,

    /// <summary>A radio button cannot be switched off by selecting it again.</summary>
    NoToggleToOff = 1 << 14,

    /// <summary>A combo box also takes typed text.</summary>
    Edit = 1 << 18,

    /// <summary>The options are sorted.</summary>
    Sort = 1 << 19,

    /// <summary>A list box allows several selections.</summary>
    MultiSelect = 1 << 21,

    /// <summary>Text is not spell checked.</summary>
    DoNotSpellCheck = 1 << 22,

    /// <summary>A text field does not scroll.</summary>
    DoNotScroll = 1 << 23,

    /// <summary>A text field is split into one box per character.</summary>
    Comb = 1 << 24,

    /// <summary>A choice field commits as soon as an option is selected.</summary>
    CommitOnSelectionChange = 1 << 26,
}
