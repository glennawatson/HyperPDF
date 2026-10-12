// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms;

/// <summary>The kinds of form field.</summary>
public enum FormFieldKind
{
    /// <summary>A kind the viewer cannot fill.</summary>
    Unknown = 0,

    /// <summary>A button that runs an action.</summary>
    PushButton = 1,

    /// <summary>A box that is ticked or not.</summary>
    CheckBox = 2,

    /// <summary>One choice out of a group.</summary>
    RadioButton = 3,

    /// <summary>A drop-down list.</summary>
    ComboBox = 4,

    /// <summary>A list of choices.</summary>
    ListBox = 5,

    /// <summary>A text box.</summary>
    Text = 6,

    /// <summary>A place for a digital signature.</summary>
    Signature = 7,
}
