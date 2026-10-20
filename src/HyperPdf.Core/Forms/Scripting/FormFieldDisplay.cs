// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>Where a field shows, as a script sets it (the values of <c>display</c> in a form script).</summary>
public enum FormFieldDisplay
{
    /// <summary>Shown on screen and in print.</summary>
    Visible = 0,

    /// <summary>Hidden on screen and in print.</summary>
    Hidden = 1,

    /// <summary>Shown on screen, left out of print.</summary>
    NoPrint = 2,

    /// <summary>Hidden on screen, shown in print.</summary>
    NoView = 3,
}
