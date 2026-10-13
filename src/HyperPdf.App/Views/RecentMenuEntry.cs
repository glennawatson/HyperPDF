// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using HyperPdf.Core.Platform;

namespace HyperPdf.App.Views;

/// <summary>A menu item of the Open Recent menu and the recent document it shows.</summary>
/// <param name="Item">The menu item.</param>
/// <param name="Recent">The recent document.</param>
internal readonly record struct RecentMenuEntry(MenuItem Item, RecentDocument Recent);
