// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms;

/// <summary>What happened when a form action ran.</summary>
[Flags]
public enum FormActionResult
{
    /// <summary>Nothing ran.</summary>
    None = 0,

    /// <summary>At least one action ran.</summary>
    Ran = 1 << 0,

    /// <summary>An action's type is not run (for example JavaScript), so it was skipped.</summary>
    Skipped = 1 << 1,

    /// <summary>The host declined an action, for example because the user said no.</summary>
    Declined = 1 << 2,

    /// <summary>An action changed field values or hid fields, so pages need drawing again and the document has unsaved changes.</summary>
    Changed = 1 << 3,

    /// <summary>An action showed or hid layers, so pages need drawing again.</summary>
    LayersChanged = 1 << 4,
}
