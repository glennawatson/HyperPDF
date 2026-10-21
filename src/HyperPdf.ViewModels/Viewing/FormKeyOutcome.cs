// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Viewing;

/// <summary>What a key did to the form fields.</summary>
public enum FormKeyOutcome
{
    /// <summary>The key was not for a form field.</summary>
    Unhandled = 0,

    /// <summary>A field took the key and the view should redraw.</summary>
    Redraw = 1,

    /// <summary>A focused list or combo box should show its choices.</summary>
    OpenChoices = 2,
}
