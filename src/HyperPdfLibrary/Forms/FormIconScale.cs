// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Forms;

/// <summary>When a push button's icon is scaled to its space (<c>/IF /SW</c>, ISO 32000-2 12.5.6.19, Table 190).</summary>
internal enum FormIconScale
{
    /// <summary>Always scale the icon (<c>A</c>).</summary>
    Always = 0,

    /// <summary>Scale the icon only when it is bigger than the space (<c>B</c>).</summary>
    WhenTooBig = 1,

    /// <summary>Scale the icon only when it is smaller than the space (<c>S</c>).</summary>
    WhenTooSmall = 2,

    /// <summary>Never scale the icon (<c>N</c>).</summary>
    Never = 3,
}
