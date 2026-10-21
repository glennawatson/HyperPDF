// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Presentation.Canvas;

/// <summary>What a press with the Select tool did.</summary>
/// <param name="Session">The move drag the press starts, or <see langword="null"/> when it starts none.</param>
/// <param name="Handled">Whether the press was used and the toolkit should not pass it on.</param>
[DebuggerDisplay("AnnotationPressOutcome: handled={Handled}")]
public readonly record struct AnnotationPressOutcome(AnnotationEditSession? Session, bool Handled);
