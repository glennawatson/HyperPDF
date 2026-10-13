// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Document;

/// <summary>The colour space a separation dictionary names.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Colorants">Its colourant names; empty for no colour space, and <see langword="null"/> while an array read is found malformed.</param>
[DebuggerDisplay("SeparationSpace: {Kind}")]
internal readonly record struct SeparationSpace(PdfSeparationSpaceKind Kind, string[]? Colorants);
