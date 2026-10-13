// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using HyperPdf.App.ViewModels;

namespace HyperPdf.App.Views;

/// <summary>A dragged tab moving onto the place of another tab.</summary>
/// <param name="Drag">The drag, which moves its start to <paramref name="Position"/> once the move is made.</param>
/// <param name="Target">The tab under the pointer.</param>
/// <param name="Position">The pointer position, in tab strip coordinates.</param>
internal readonly record struct TabReorder(TabDrag Drag, DocumentTabViewModel Target, Point Position);
