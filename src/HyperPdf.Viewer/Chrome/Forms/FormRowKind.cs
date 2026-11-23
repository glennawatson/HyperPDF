// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>What one row of a form dialog is.</summary>
internal enum FormRowKind
{
    /// <summary>Bold text that names a group of rows.</summary>
    Heading = 0,

    /// <summary>Plain text, wrapped over as many lines as it needs.</summary>
    Text = 1,

    /// <summary>A setting that is on or off.</summary>
    Toggle = 2,

    /// <summary>A setting with a list of values, one of which is chosen.</summary>
    Choice = 3,

    /// <summary>A setting typed as text.</summary>
    Field = 4,

    /// <summary>Something to do, run by a press.</summary>
    Action = 5,

    /// <summary>A few values that are all shown at once, one of which is chosen by a press.</summary>
    Options = 6,
}
