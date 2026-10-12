// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.ViewModels;

/// <summary>Asks for the selected text to be marked.</summary>
/// <param name="Kind">The markup kind.</param>
/// <param name="Color">The colour.</param>
/// <param name="Lines">The selected line rectangles by page.</param>
[DebuggerDisplay("MarkSelectionRequest: {Kind}")]
public readonly record struct MarkSelectionRequest(AnnotationKind Kind, uint Color, IReadOnlyDictionary<int, List<PageRect>> Lines);
