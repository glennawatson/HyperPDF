// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Windowing.Windows;

/// <summary>
/// A window whose operating system draws the application's menu bar itself, as macOS does at the top of the screen. The viewer
/// then hands it the menus and draws no menu bar of its own.
/// </summary>
public interface INativeMenuBar
{
    /// <summary>Shows the menus. Each menu's entries are made when it opens, so they follow what the document allows at that moment.</summary>
    /// <param name="titles">The menus' names, in order, without access marks.</param>
    /// <param name="build">Makes the entries of the menu at a place; called each time that menu opens.</param>
    /// <param name="application">The words of the application menu.</param>
    void SetMenus(IReadOnlyList<string> titles, Func<int, NativeMenuItem[]> build, NativeAppMenu application);
}
