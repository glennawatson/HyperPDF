// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
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

    /// <summary>Guards the latest token, which is too large for an atomic exchange.</summary>
    private readonly Lock _tokenGate = new();

    /// <summary>The checks made so far.</summary>
    private int _checks;

    /// <summary>The token of the latest check.</summary>
    private CancellationToken _lastToken;

    /// <summary>The checks cancelled while held.</summary>
    private int _cancelled;

    /// <inheritdoc/>
    public bool IsActive { get; private set; } = isActive;

    /// <inheritdoc/>
    public AsObservableSignal<RxVoid> Activations => new(_activations);

    /// <inheritdoc/>
    public long Announcements => 0;

    /// <summary>Gets the number of checks made.</summary>
    internal int Checks => Volatile.Read(ref _checks);

    /// <summary>Gets the number of checks that were cancelled while held.</summary>
    internal int Cancelled => Volatile.Read(ref _cancelled);

    /// <summary>Gets or sets a gate each check waits at until it is released or cancelled; <see langword="null"/> answers at once.</summary>
    internal TaskCompletionSource? Hold { get; set; }

    /// <summary>Gets what happened as each check started: the check's number, and whether the previous check's token was already cancelled.</summary>
    internal ConcurrentQueue<string> Events { get; } = new();

    /// <inheritdoc/>
    public Task<bool> PrepareRepeatAsync(long announcementsBefore, CancellationToken cancellationToken)
    {
        var number = Interlocked.Increment(ref _checks);
        CancellationToken previous;
        lock (_tokenGate)
        {
            previous = _lastToken;
            _lastToken = cancellationToken;
        }

        Events.Enqueue(number == 1 ? "entered 1" : $"entered {number}, previous cancelled: {previous.IsCancellationRequested}");
        return Hold is { } hold ? WaitHeldAsync(hold, cancellationToken) : Task.FromResult(true);
    }

    /// <inheritdoc/>
    public void Dispose() => _activations.Dispose();

    /// <summary>Starts listening, as a screen reader started after the app would.</summary>
    internal void Activate()
    {
        IsActive = true;
        _activations.OnNext(RxVoid.Default);
    }

    /// <summary>Waits at the gate, counting a cancellation.</summary>
    /// <param name="hold">The gate.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>Always <see langword="true"/> once released.</returns>
    private async Task<bool> WaitHeldAsync(TaskCompletionSource hold, CancellationToken cancellationToken)
    {
        try
        {
            await hold.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            _ = Interlocked.Increment(ref _cancelled);
            throw;
        }
    }
}
