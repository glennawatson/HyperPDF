// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>An entry of the document outline, taken from a heading.</summary>
/// <param name="Level">The level, from 1.</param>
/// <param name="Title">The title.</param>
/// <param name="Destination">The name of the place it jumps to.</param>
[DebuggerDisplay("OutlineEntry: {Level} {Title}")]
internal readonly record struct OutlineEntry(int Level, string Title, string Destination);
