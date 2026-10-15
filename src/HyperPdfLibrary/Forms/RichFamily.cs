// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Forms;

/// <summary>The kind of font a CSS family list asks for; rich text is drawn in the closest of the standard 14 fonts.</summary>
internal enum RichFamily
{
    /// <summary>No family is named, so the field's own font is used.</summary>
    Inherit = 0,

    /// <summary>A sans serif family, drawn in Helvetica.</summary>
    Sans = 1,

    /// <summary>A serif family, drawn in Times.</summary>
    Serif = 2,

    /// <summary>A fixed width family, drawn in Courier.</summary>
    Mono = 3,
}
