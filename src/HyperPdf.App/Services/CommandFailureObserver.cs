// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.App.ViewModels;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Services;

/// <summary>
/// Receives the exceptions a command throws that nobody observed. ReactiveUI's own default answer is to throw on the
/// main thread, which ends the app and loses the unsaved edits of every tab; this one traces the error and tells the
/// reader in plain words instead.
/// </summary>
internal sealed class CommandFailureObserver : IObserver<Exception>
{
    /// <summary>Where the plain words go, or <see langword="null"/> before a window is up.</summary>
    private Action<string>? _show;

    /// <summary>Gets the observer the app hands to ReactiveUI.</summary>
    internal static CommandFailureObserver Instance { get; } = new();

    /// <inheritdoc/>
    public void OnNext(Exception value)
    {
        Trace.TraceError(value.ToString());
        Volatile.Read(ref _show)?.Invoke(Describe(value));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <inheritdoc/>
    public void OnCompleted()
    {
        // The handler lasts as long as the app.
    }

    /// <summary>Describes a failure in plain words.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The text to show.</returns>
    internal static string Describe(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return CopyFailure.IsDocumentClosed(error)
            ? "That action stopped because the document was closed or reloaded."
            : $"That action did not finish: {error.Message}";
    }

    /// <summary>Sends the plain words of later failures to a window.</summary>
    /// <param name="show">Shows the words.</param>
    /// <returns>Stops sending when disposed.</returns>
    internal IDisposable Attach(Action<string> show)
    {
        ArgumentNullException.ThrowIfNull(show);
        Volatile.Write(ref _show, show);
        return Scope.Create(this, static self => Volatile.Write(ref self._show, null));
    }
}
