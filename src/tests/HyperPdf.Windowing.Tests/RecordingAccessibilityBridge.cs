// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;
using HyperPdf.Windowing.Accessibility;

namespace HyperPdf.Windowing.Tests;

/// <summary>An accessibility bridge for tests that remembers what a window published.</summary>
internal sealed class RecordingAccessibilityBridge : IAccessibilityBridge
{
    /// <inheritdoc/>
    public bool IsAssistiveTechnologyActive => false;

    /// <summary>Gets a value indicating whether the bridge was disposed.</summary>
    internal bool Disposed { get; private set; }

    /// <summary>Gets the tree the bridge was given, or <see langword="null"/> when none was attached.</summary>
    internal IAccessibleTree? Tree { get; private set; }

    /// <inheritdoc/>
    public void Attach(IAccessibleTree tree) => Tree = tree;

    /// <inheritdoc/>
    public void Dispose() => Disposed = true;
}
