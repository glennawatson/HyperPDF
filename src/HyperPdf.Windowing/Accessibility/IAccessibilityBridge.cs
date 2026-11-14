// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.Windowing.Accessibility;

/// <summary>
/// Publishes the accessible tree of a window to the platform: UI Automation on Windows (which JAWS, NVDA and ZoomText read),
/// NSAccessibility on macOS and AT-SPI on Linux. One implementation lives in each platform head. They all read the same
/// <see cref="IAccessibleTree"/> the view models keep.
/// </summary>
public interface IAccessibilityBridge : IDisposable
{
    /// <summary>Gets a value indicating whether an assistive technology is attached and reading the window.</summary>
    bool IsAssistiveTechnologyActive { get; }

    /// <summary>Starts serving a tree to the platform. The bridge reads the tree on demand and follows its changes.</summary>
    /// <param name="tree">The tree.</param>
    void Attach(IAccessibleTree tree);
}
