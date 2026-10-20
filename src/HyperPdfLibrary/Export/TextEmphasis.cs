// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Export;

/// <summary>The emphasis a run of exported text carries.</summary>
[Flags]
internal enum TextEmphasis
{
    /// <summary>The run has no emphasis at all.</summary>
    None = 0,

    /// <summary>The run is set in a bold font.</summary>
    Bold = 1 << 0,

    /// <summary>The run is set in an italic font.</summary>
    Italic = 1 << 1,

    /// <summary>The run has an underline drawn under it.</summary>
    Underline = 1 << 2,
}
