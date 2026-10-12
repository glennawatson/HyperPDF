// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Platform;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Tests;

/// <summary>A focus announcement check that reports every focus change as unheard, as Avalonia's Linux bridge does for controls no screen reader has read.</summary>
/// <param name="isActive">Whether a screen reader is listening.</param>
[DebuggerDisplay("FakeFocusAnnouncementCheck: {Checks}")]
internal sealed class FakeFocusAnnouncementCheck(bool isActive) : IFocusAnnouncementCheck
{
    /// <summary>Signals when a screen reader starts listening.</summary>
    private readonly Signal<RxVoid> _activations = new();

    /// <summary>The checks made so far.</summary>
    private int _checks;

    /// <inheritdoc/>
    public bool IsActive { get; private set; } = isActive;

    /// <inheritdoc/>
    public AsObservableSignal<RxVoid> Activations => new(_activations);

    /// <inheritdoc/>
    public long Announcements => 0;

    /// <summary>Gets the number of checks made.</summary>
    internal int Checks => Volatile.Read(ref _checks);

    /// <inheritdoc/>
    public Task<bool> PrepareRepeatAsync(long announcementsBefore, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _checks);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public void Dispose() => _activations.Dispose();

    /// <summary>Starts listening, as a screen reader started after the app would.</summary>
    internal void Activate()
    {
        IsActive = true;
        _activations.OnNext(RxVoid.Default);
    }
}
