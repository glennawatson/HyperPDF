// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A piece of one line of rich text that is in a single run, and where it is drawn.</summary>
/// <param name="Run">The run the piece belongs to.</param>
/// <param name="First">The place of the piece's first unit in the run.</param>
/// <param name="Count">The number of units.</param>
/// <param name="X">The x position of the piece's start.</param>
/// <param name="Baseline">The y position of the line's baseline.</param>
[DebuggerDisplay("RichPiece: run {Run} {First}+{Count} at {X}, {Baseline}")]
internal readonly record struct RichPiece(int Run, int First, int Count, float X, float Baseline);
