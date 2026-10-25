// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.ViewModels.Services;

/// <summary>
/// Receives the exceptions a command throws that nobody observed. ReactiveUI's own default answer is to throw on the
/// main thread, which ends the app and loses the unsaved edits of every tab; this one traces the error and tells the
/// reader in plain words instead.
/// </summary>
public static class CommandFailures
{
    /// <summary>Where the plain words go, or <see langword="null"/> before a window is up.</summary>
    private static Action<string>? _show;

    /// <summary>Gets the observer the app hands to ReactiveUI as its exception handler.</summary>
    public static IObserver<Exception> Observer { get; } = Witness.Create<Exception>(Report, ErrorTrace.Write);

    /// <summary>Sends the plain words of later failures to a window.</summary>
    /// <param name="show">Shows the words.</param>
    /// <returns>Stops sending when disposed.</returns>
    public static IDisposable ShowIn(Action<string> show)
    {
        ArgumentNullException.ThrowIfNull(show);
        Volatile.Write(ref _show, show);
        return Scope.Create(static () => Volatile.Write(ref _show, null));
    }

    /// <summary>Describes a failure in plain words.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The text to show.</returns>
    internal static string Describe(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return CopyFailure.IsDocumentClosed(error)
            ? UiText.Get("That action stopped because the document was closed or reloaded.")
            : UiText.Format("That action did not finish: {0}", error.Message);
    }

    /// <summary>Traces a failure and shows its plain words in the window, when there is one.</summary>
    /// <param name="error">The failure.</param>
    private static void Report(Exception error)
    {
        ErrorTrace.Write(error);
        Volatile.Read(ref _show)?.Invoke(Describe(error));
    }
}
