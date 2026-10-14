// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;

namespace HyperPdf.App.Views;

/// <summary>A list item's container that has just been prepared, with the list it belongs to.</summary>
/// <param name="List">The list.</param>
/// <param name="Args">The container and its position.</param>
internal readonly record struct PreparedItem(ItemsControl List, ContainerPreparedEventArgs Args);
