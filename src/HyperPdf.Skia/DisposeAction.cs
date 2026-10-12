// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;

namespace HyperPdf.Skia;

/// <summary>Runs an owned cleanup action when disposed.</summary>
/// <param name = "action">The cleanup callback.</param>
internal sealed class DisposeAction(Action action) : IDisposable
{
    /// <summary>The action.</summary>
    private Action? _action = action;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();

    /// <summary>Creates a disposable cleanup callback.</summary>
    /// <param name = "action">The cleanup callback.</param>
    /// <returns>The callback owner.</returns>
    internal static DisposeAction Create(Action action) => new(action);
}
