// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Windowing.Accessibility;

namespace HyperPdf.Windowing.Tests;

/// <summary>An accessibility bridge for tests that remembers what a window published.</summary>
internal sealed class RecordingAccessibilityBridge : IAccessibilityBridge
{
    /// <inheritdoc/>
    public bool IsAssistiveTechnologyActive => false;

    /// <summary>Gets a value indicating whether the bridge was disposed.</summary>
    internal bool Disposed { get; private set; }

    /// <inheritdoc/>
    public void Publish(ReadOnlySpan<AccessibleNode> nodes)
    {
        // The tests only check that windows own and dispose their bridge.
    }

    /// <inheritdoc/>
    public void Notify(int id, AccessibleChange change)
    {
        // The tests only check that windows own and dispose their bridge.
    }

    /// <inheritdoc/>
    public void CaretMoved(in AccessibleBounds bounds)
    {
        // The tests only check that windows own and dispose their bridge.
    }

    /// <inheritdoc/>
    public void Dispose() => Disposed = true;
}
