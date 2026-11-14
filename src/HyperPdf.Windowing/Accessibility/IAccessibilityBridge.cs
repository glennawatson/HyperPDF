// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Windowing.Accessibility;

/// <summary>
/// Publishes the accessible tree of a window to the platform: UI Automation on Windows (which JAWS, NVDA and ZoomText read),
/// NSAccessibility on macOS and AT-SPI on Linux. One implementation lives in each platform head.
/// </summary>
public interface IAccessibilityBridge : IDisposable
{
    /// <summary>Gets a value indicating whether an assistive technology is attached and reading the window.</summary>
    bool IsAssistiveTechnologyActive { get; }

    /// <summary>Replaces the tree the platform sees.</summary>
    /// <param name="nodes">The nodes in tree order, parents before children.</param>
    void Publish(ReadOnlySpan<AccessibleNode> nodes);

    /// <summary>Tells the platform one node changed, so a reader can speak it.</summary>
    /// <param name="id">The node.</param>
    /// <param name="change">What changed.</param>
    void Notify(int id, AccessibleChange change);

    /// <summary>Tells the platform where the caret is, so a magnifier can follow it.</summary>
    /// <param name="bounds">The caret bounds in window device pixels.</param>
    void CaretMoved(in AccessibleBounds bounds);
}
