// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>The values of the /UseBlackPtComp graphics state entry (ISO 32000-2 Table 57 and 8.6.5.9).</summary>
internal enum BlackPointCompensationMode
{
    /// <summary>/Default, the initial value: the PDF processor decides. HyperPDF compensates, as <see cref="On"/> does.</summary>
    Default = 0,

    /// <summary>/ON: CIE-based conversions compensate for black points as ISO 18619 describes.</summary>
    On = 1,

    /// <summary>/OFF: no conversion compensates for black points.</summary>
    Off = 2,
}
