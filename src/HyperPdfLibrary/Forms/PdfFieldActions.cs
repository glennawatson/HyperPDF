// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// The actions a field carries, kept as data (ISO 32000-2 12.6.3 and 12.7.8). The library stores them and never runs
/// them. A document that claims PDF/A cannot take any.
/// </summary>
[DebuggerDisplay("PdfFieldActions")]
public sealed record PdfFieldActions
{
    /// <summary>Gets the JavaScript run for each keystroke (<c>/AA /K</c>), or null for none.</summary>
    public string? Keystroke { get; init; }

    /// <summary>Gets the JavaScript that formats the value for display (<c>/AA /F</c>), or null for none.</summary>
    public string? Format { get; init; }

    /// <summary>Gets the JavaScript that checks the value (<c>/AA /V</c>), or null for none.</summary>
    public string? Validate { get; init; }

    /// <summary>Gets the JavaScript that works out the value from other fields (<c>/AA /C</c>), or null for none. A field with one joins the form's calculation order.</summary>
    public string? Calculate { get; init; }

    /// <summary>Gets the JavaScript a push button runs when it is clicked (widget <c>/A</c>), or null for none.</summary>
    public string? Activate { get; init; }

    /// <summary>Gets the address a push button opens when it is clicked (widget <c>/A</c>, a URI action), or null for none. <see cref="Activate"/> wins when both are set.</summary>
    public string? ActivateUri { get; init; }

    /// <summary>Gets a value indicating whether no action is set.</summary>
    public bool IsEmpty =>
        Keystroke is null && Format is null && Validate is null && Calculate is null && Activate is null && ActivateUri is null;
}
