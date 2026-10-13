// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Requirements;

/// <summary>One requirement dictionary from the catalog's <c>/Requirements</c> array (ISO 32000-2 §12.11.1, Table 273).</summary>
/// <param name="Type">The requirement type (<c>/S</c>), such as <c>Transitions</c>. It may be a type this library does not know.</param>
/// <param name="Version">The <c>/V</c> entry, or <see langword="null"/> when the requirement is to be met without regard to version.</param>
/// <param name="Penalty">The penalty from 0 to 100 added when the requirement is not met (<c>/Penalty</c>, default 100).</param>
/// <param name="Handlers">The alternative requirement handlers named in <c>/RH</c>. They are data only and are never run.</param>
/// <param name="Encryption">For an <c>Encryption</c> requirement, the settings it asks for; otherwise <see langword="null"/>.</param>
[DebuggerDisplay("PdfRequirement: {Type} penalty {Penalty}")]
public sealed record PdfRequirement(
    string Type,
    PdfRequirementVersion? Version,
    int Penalty,
    IReadOnlyList<PdfRequirementHandler> Handlers,
    PdfRequirementEncryption? Encryption);
