// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using HyperPdf.App.Controls;
using HyperPdf.Core.Platform;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>
/// Repeats focus changes the screen reader never heard. Avalonia's Linux bridge drops the announcement when focus moves
/// to a control no screen reader has read yet, such as one in a pane that was hidden or has just been built. When the
/// check finds a change went unannounced, focus moves away and straight back, which the bridge then announces.
/// </summary>
[DebuggerDisplay("FocusAnnouncementRepair")]
internal sealed class FocusAnnouncementRepair : IDisposable
{
    /// <summary>The check that sees the announcements.</summary>
    private readonly IFocusAnnouncementCheck _check;

    /// <summary>Lists the app's windows, to find the focused control when a screen reader starts listening.</summary>
    private readonly Func<IEnumerable<TopLevel>> _windows;

    /// <summary>Owns the chain that checks and repeats focus changes.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The control focus was last repeated on, so a repeat that is still unheard is not repeated again.</summary>
    private IInputElement? _repeated;

    /// <summary>Initializes a new instance of the <see cref="FocusAnnouncementRepair"/> class.</summary>
    /// <param name="check">The check that sees the announcements.</param>
    /// <param name="windows">Lists the app's windows.</param>
    internal FocusAnnouncementRepair(IFocusAnnouncementCheck check, Func<IEnumerable<TopLevel>> windows)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(windows);
        _check = check;
        _windows = windows;

        // A focus change, or a screen reader starting to listen, starts a check. A newer change replaces the walk for
        // the older one, which SwitchMap cancels; an unheard change is repeated on the UI thread.
        _subscriptions.Add(Signal.Merge(
                InputElement.FocusArrivals.Select(ReadFocusChange).KeepNotNull(),
                check.Activations.ObserveOn(AvaloniaScheduler.Instance).Select(_ => ReadFocusedControl()).KeepNotNull())
            .Do(ForgetEarlierRepeat)
            .SwitchMap(static change => change.WhenUnheard())
            .ObserveOn(AvaloniaScheduler.Instance)
            .SubscribeSafe(static change => change.Repeat(), ErrorTrace.Write));
    }

    /// <summary>
    /// Gets a value indicating whether focus is moving away and back for a repeat. Code that acts when a control loses
    /// focus, such as committing a form field, ignores the loss while this is set.
    /// </summary>
    internal static bool IsRepeating { get; private set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Moves focus away and back, keeping a text box's caret and selection.</summary>
    /// <param name="topLevel">The focused control's window.</param>
    /// <param name="element">The focused control.</param>
    /// <param name="method">How focus first arrived, so the focus outline looks the same.</param>
    internal static void Refocus(TopLevel topLevel, InputElement element, NavigationMethod method)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        ArgumentNullException.ThrowIfNull(element);
        var text = element as TextBox;
        var selection = text is null ? default : new TextSelection(text.SelectionStart, text.SelectionEnd, text.CaretIndex);
        IsRepeating = true;
        try
        {
            _ = topLevel.FocusManager?.Focus(null);
            _ = element.Focus(method);
        }
        finally
        {
            IsRepeating = false;
        }

        if (text is null)
        {
            return;
        }

        text.CaretIndex = selection.Caret;
        text.SelectionStart = selection.Start;
        text.SelectionEnd = selection.End;
    }

    /// <summary>
    /// Reads the focus change worth checking from one step of a focus event, once a screen reader is listening. Focus
    /// that arrives with a pointer press is left alone: moving it away would release the press, so the click would be lost.
    /// Only the step on the focused control itself counts, so the change is read once, not once per ancestor.
    /// </summary>
    /// <param name="arrival">The control the step reached and the event.</param>
    /// <returns>The change to check, or <see langword="null"/>; nothing is allocated while no screen reader listens.</returns>
    internal FocusChange? ReadFocusChange(FocusArrival arrival) =>
        !_check.IsActive || arrival.Args is not FocusChangedEventArgs { NewFocusedElement: InputElement element } focus
            || focus.NavigationMethod == NavigationMethod.Pointer || !ReferenceEquals(arrival.Sender, element)
            || TopLevel.GetTopLevel(element) is not { } topLevel || ReferenceEquals(element, topLevel)
            ? null
            : new(this, topLevel, element, focus.NavigationMethod, _check.Announcements, !ReferenceEquals(element, _repeated));

    /// <summary>
    /// Reads the control that has focus in the active window, once a screen reader starts listening: it may have taken
    /// focus before the check could see the bridge, or before the screen reader started. How focus arrived is no longer
    /// known, so a repeat uses keyboard navigation and the focus outline stays visible.
    /// </summary>
    /// <returns>The change to check, or <see langword="null"/> when no window holds focus.</returns>
    private FocusChange? ReadFocusedControl()
    {
        if (!_check.IsActive)
        {
            return null;
        }

        foreach (var window in _windows())
        {
            if (window is WindowBase { IsActive: true } && window.FocusManager?.GetFocusedElement() is InputElement element && !ReferenceEquals(element, window))
            {
                return new(this, window, element, NavigationMethod.Tab, _check.Announcements, true);
            }
        }

        return null;
    }

    /// <summary>Forgets the control focus was last repeated on once focus has moved to another one.</summary>
    /// <param name="change">The new focus change.</param>
    private void ForgetEarlierRepeat(FocusChange change)
    {
        if (change.IsFresh)
        {
            _repeated = null;
        }
    }

    /// <summary>Waits for the announcement of a focus change.</summary>
    /// <param name="announcementsBefore">The announcements counted before the change.</param>
    /// <param name="cancellationToken">Cancelled when a newer focus change replaces this one.</param>
    /// <returns><see langword="true"/> when the change went unheard and should be repeated.</returns>
    private async Task<bool> IsUnheardAsync(long announcementsBefore, CancellationToken cancellationToken)
    {
        try
        {
            return await _check.PrepareRepeatAsync(announcementsBefore, cancellationToken).ConfigureAwait(false) && !cancellationToken.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer focus change replaced this check.
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A failed check leaves focus as the bridge reported it; the user can still move on.
            ErrorTrace.Write(ex);
            return false;
        }
    }

    /// <summary>Repeats a focus change once, while the control still has focus.</summary>
    /// <param name="change">The focus change.</param>
    private void Repeat(FocusChange change)
    {
        if (ReferenceEquals(change.Element, _repeated) || !ReferenceEquals(change.TopLevel.FocusManager?.GetFocusedElement(), change.Element))
        {
            return;
        }

        _repeated = change.Element;
        Refocus(change.TopLevel, change.Element, change.Method);
    }

    /// <summary>A text box's selection and caret, kept while focus moves away and back.</summary>
    /// <param name="Start">The selection start.</param>
    /// <param name="End">The selection end.</param>
    /// <param name="Caret">The caret index.</param>
    private readonly record struct TextSelection(int Start, int End, int Caret);

    /// <summary>A focus change waiting for its announcement.</summary>
    /// <param name="Owner">The repair that saw it.</param>
    /// <param name="TopLevel">The window.</param>
    /// <param name="Element">The focused control.</param>
    /// <param name="Method">How focus arrived.</param>
    /// <param name="AnnouncementsBefore">The announcements counted before the change.</param>
    /// <param name="IsFresh">Whether the change is to a control other than the one last repeated, which is then forgotten.</param>
    internal sealed record FocusChange(FocusAnnouncementRepair Owner, TopLevel TopLevel, InputElement Element, NavigationMethod Method, long AnnouncementsBefore, bool IsFresh)
    {
        /// <summary>
        /// Gets the change once its announcement was missed; nothing arrives when it was heard. The walk starts on the
        /// UI thread's next turn, because SwitchMap subscribes the newer walk before it disposes the older one, and the
        /// older walk's token must be cancelled first.
        /// </summary>
        /// <returns>The change, at most once.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<FocusChange> WhenUnheard() =>
            Signal.FromAsync(IsUnheardAsync).SubscribeOn(AvaloniaScheduler.Instance).Where(static unheard => unheard).SelectWith(this, static (change, _) => change);

        /// <summary>Repeats the change; runs on the UI thread.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Repeat() => Owner.Repeat(this);

        /// <summary>Waits for the change's announcement.</summary>
        /// <param name="cancellationToken">Cancelled when a newer change replaces this one.</param>
        /// <returns><see langword="true"/> when it went unheard.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Task<bool> IsUnheardAsync(CancellationToken cancellationToken) => Owner.IsUnheardAsync(AnnouncementsBefore, cancellationToken);
    }
}
