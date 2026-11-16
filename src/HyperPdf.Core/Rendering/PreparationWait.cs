// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Rendering;

/// <summary>Tells whether a page's preparation finished while it was being subscribed to, or after the subscription returned.</summary>
internal sealed class PreparationWait
{
    /// <summary>The subscription has not returned and the page has not arrived.</summary>
    private const int Subscribing = 0;

    /// <summary>The page arrived while the subscription was still being made.</summary>
    private const int ArrivedAtOnce = 1;

    /// <summary>The subscription returned before the page arrived.</summary>
    private const int Waiting = 2;

    /// <summary>The phase.</summary>
    private int _phase;

    /// <summary>Records that the page arrived.</summary>
    /// <returns><see langword="true"/> when the subscription had already returned, so the arrival must be acted on; <see langword="false"/> when the caller that is subscribing acts on it.</returns>
    internal bool ArrivedAfterReturn() => Interlocked.CompareExchange(ref _phase, ArrivedAtOnce, Subscribing) != Subscribing;

    /// <summary>Records that the subscription returned.</summary>
    /// <returns><see langword="true"/> when the page had already arrived.</returns>
    internal bool ReadyBeforeReturn() => Interlocked.CompareExchange(ref _phase, Waiting, Subscribing) == ArrivedAtOnce;
}
