// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Icons;

/// <summary>What kind of action an icon stands for, which picks its tint. The shapes differ too, so colour is never the only cue.</summary>
public enum IconTone
{
    /// <summary>No kind: drawn in the text colour.</summary>
    Plain = 0,

    /// <summary>Moving about or changing the view.</summary>
    Navigation = 1,

    /// <summary>Adding, opening or saving.</summary>
    Add = 2,

    /// <summary>Changing the document.</summary>
    Edit = 3,

    /// <summary>Removing or closing.</summary>
    Remove = 4,
}
