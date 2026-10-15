// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Forms;

/// <summary>How a font's codes are laid out.</summary>
internal enum FormCodeLayout
{
    /// <summary>Every code is one byte.</summary>
    Bytes = 0,

    /// <summary>Every code is two bytes.</summary>
    Wide = 1,

    /// <summary>Codes are one or two bytes, as the CMap's codespace says.</summary>
    Mixed = 2,

    /// <summary>A code is the character itself, in two bytes (the Unicode CMaps).</summary>
    Unicode = 3,
}
