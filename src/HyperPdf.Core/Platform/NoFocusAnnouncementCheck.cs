// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Platform;

/// <summary>The check for platforms whose screen reader bridge announces every focus change.</summary>
[DebuggerDisplay("NoFocusAnnouncementCheck")]
public sealed class NoFocusAnnouncementCheck : IFocusAnnouncementCheck
{
    /// <summary>Gets the shared instance.</summary>
    public static NoFocusAnnouncementCheck Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsActive => false;

    /// <inheritdoc/>
    public AsObservableSignal<RxVoid> Activations { get; } = new(Signal.Never<RxVoid>());

    /// <inheritdoc/>
    public long Announcements => 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> PrepareRepeatAsync(long announcementsBefore, CancellationToken cancellationToken) => Task.FromResult(false);

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
