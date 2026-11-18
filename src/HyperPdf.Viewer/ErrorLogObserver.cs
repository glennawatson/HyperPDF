// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.ViewModels.Services;

namespace HyperPdf.Viewer;

/// <summary>
/// The handler for errors that a command or a property subscriber throws and nobody observes. ReactiveUI's own handler ends the
/// program with an unhandled exception; this one logs the error and lets the window carry on.
/// </summary>
internal sealed class ErrorLogObserver : IObserver<Exception>
{
    /// <summary>Gets the one handler; it holds no state.</summary>
    internal static ErrorLogObserver Instance { get; } = new();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnNext(Exception value) => ErrorTrace.Write(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnError(Exception error) => ErrorTrace.Write(error);

    /// <inheritdoc/>
    public void OnCompleted()
    {
    }
}
