// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Input;

namespace HyperPdf.App.Controls;

/// <summary>A request for the context menu of one item in a list.</summary>
/// <typeparam name="TItem">The item type.</typeparam>
/// <param name="Args">The context request.</param>
/// <param name="Source">The control the request came from, which a menu opens on.</param>
/// <param name="Item">The item the control shows.</param>
internal readonly record struct ItemContextRequest<TItem>(ContextRequestedEventArgs Args, Control Source, TItem Item);
