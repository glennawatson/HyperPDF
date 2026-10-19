// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>Turns layers on or off.</summary>
/// <param name="Changes">The steps, in order.</param>
/// <param name="PreserveRadioButtons">Whether radio-button groups stay consistent (<c>/PreserveRB</c>, default true).</param>
[DebuggerDisplay("SetOcgStateAction: {Changes.Length} changes")]
public sealed record SetOcgStateAction(PdfOcgStateChange[] Changes, bool PreserveRadioButtons);
