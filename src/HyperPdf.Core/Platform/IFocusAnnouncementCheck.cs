// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;

namespace HyperPdf.Core.Platform;

/// <summary>
/// Checks that keyboard focus changes reach the screen reader. Avalonia's Linux bridge announces focus only on objects
/// a screen reader has already read, so moving focus into a pane that was hidden or newly built says nothing.
/// </summary>
public interface IFocusAnnouncementCheck : IDisposable
{
    /// <summary>Gets a value indicating whether a screen reader is listening, so focus changes need checking.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets a signal each time <see cref="IsActive"/> becomes true, for example when a screen reader starts after the
    /// app. Focus that was already in place then needs checking. It may arrive on any thread.
    /// </summary>
    AsObservableSignal<RxVoid> Activations { get; }

    /// <summary>Gets the number of focus announcements the screen reader has been sent.</summary>
    long Announcements { get; }

    /// <summary>
    /// Waits for the announcement of a focus change made after <see cref="Announcements"/> read
    /// <paramref name="announcementsBefore"/>. When none was sent, makes every object readable so a repeated focus change
    /// is announced.
    /// </summary>
    /// <param name="announcementsBefore">The value of <see cref="Announcements"/> before focus changed.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true"/> when the change was not announced and focus should move away and back.</returns>
    Task<bool> PrepareRepeatAsync(long announcementsBefore, CancellationToken cancellationToken);
}
