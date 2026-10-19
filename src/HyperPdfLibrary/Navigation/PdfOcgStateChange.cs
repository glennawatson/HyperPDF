// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Navigation;

/// <summary>One step of a set-OCG-state action: a mode and the layers it applies to.</summary>
/// <param name="Mode">The mode: ON, OFF or Toggle.</param>
/// <param name="Groups">The optional content group objects.</param>
[DebuggerDisplay("PdfOcgStateChange: {Mode} {Groups.Length} groups")]
public sealed record PdfOcgStateChange(string Mode, PdfObjectId[] Groups);
