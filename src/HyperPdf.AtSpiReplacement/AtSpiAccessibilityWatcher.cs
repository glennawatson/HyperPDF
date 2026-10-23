// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Avalonia.FreeDesktop.AtSpi;

/// <summary>
/// Stands in for the watcher that tells Avalonia's X11 backend when assistive technology is switched on. It never says so, because
/// HyperPDF serves screen readers itself, so Avalonia's own AT-SPI server is never asked to show a second tree.
/// </summary>
[DebuggerDisplay("AtSpiAccessibilityWatcher: off")]
public sealed class AtSpiAccessibilityWatcher : IAsyncDisposable
{
    /// <summary>Occurs when assistive technology is switched on or off; it never does.</summary>
    public event EventHandler<bool>? IsEnabledChanged
    {
        add { }
        remove { }
    }

    /// <summary>Gets a value indicating whether assistive technology is on, which is never.</summary>
    public bool IsEnabled => false;

    /// <summary>Starts watching, which here means doing nothing.</summary>
    /// <returns>A finished task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InitAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
