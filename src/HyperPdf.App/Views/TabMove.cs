// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Input;

namespace HyperPdf.App.Views;

/// <summary>A pointer move over the tab strip and the tab drag it belongs to.</summary>
/// <param name="Move">The pointer move.</param>
/// <param name="Drag">The drag in progress, or <see langword="null"/> when no tab is being dragged.</param>
internal readonly record struct TabMove(PointerEventArgs Move, TabDrag? Drag);
