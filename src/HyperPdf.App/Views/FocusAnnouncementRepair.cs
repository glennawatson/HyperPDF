// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using HyperPdf.Core.Platform;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
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

    /// <summary>The subscription to focus changes in every window.</summary>
    private readonly IDisposable _subscription;

    /// <summary>The subscription to the check becoming active.</summary>
    private readonly IDisposable _activations;

    /// <summary>Lists the app's windows, to find the focused control when a screen reader starts listening.</summary>
    private readonly Func<IEnumerable<TopLevel>> _windows;

    /// <summary>The control focus was last repeated on, so a repeat that is still unheard is not repeated again.</summary>
    private IInputElement? _repeated;

    /// <summary>Stops the check for the latest focus change.</summary>
    private CancellationTokenSource? _checking;

    /// <summary>Initializes a new instance of the <see cref="FocusAnnouncementRepair"/> class.</summary>
    /// <param name="check">The check that sees the announcements.</param>
    /// <param name="windows">Lists the app's windows.</param>
    internal FocusAnnouncementRepair(IFocusAnnouncementCheck check, Func<IEnumerable<TopLevel>> windows)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(windows);
        _check = check;
        _windows = windows;
        _subscription = InputElement.GotFocusEvent.Raised.SubscribeSafe(OnGotFocus, OnError);
        _activations = check.Activations.SubscribeSafe(_ => OnActivated(), OnError);
    }

    /// <summary>
    /// Gets a value indicating whether focus is moving away and back for a repeat. Code that acts when a control loses
    /// focus, such as committing a form field, ignores the loss while this is set.
    /// </summary>
    internal static bool IsRepeating { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscription.Dispose();
        _activations.Dispose();
        _ = Interlocked.Exchange(ref _checking, null)?.CancelAsync();
    }

    /// <summary>Moves focus away and back, keeping a text box's caret and selection.</summary>
    /// <param name="topLevel">The focused control's window.</param>
    /// <param name="element">The focused control.</param>
    /// <param name="method">How focus first arrived, so the focus outline looks the same.</param>
    internal static void Refocus(TopLevel topLevel, InputElement element, NavigationMethod method)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        ArgumentNullException.ThrowIfNull(element);
        var text = element as TextBox;
        var (start, end, caret) = text is null ? default : (text.SelectionStart, text.SelectionEnd, text.CaretIndex);
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

        text.CaretIndex = caret;
        text.SelectionStart = start;
        text.SelectionEnd = end;
    }

    /// <summary>
    /// Starts checking a focus change once a screen reader is listening. Focus that arrives with a pointer press is left
    /// alone: moving it away would release the press, so the click would be lost.
    /// </summary>
    /// <param name="raised">The control that raised the event and the event.</param>
    internal void OnGotFocus((object Sender, RoutedEventArgs Args) raised)
    {
        if (!_check.IsActive || raised.Args is not FocusChangedEventArgs { NewFocusedElement: InputElement element } focus
            || focus.NavigationMethod == NavigationMethod.Pointer || !ReferenceEquals(raised.Sender, element)
            || TopLevel.GetTopLevel(element) is not { } topLevel || ReferenceEquals(element, topLevel))
        {
            return;
        }

        if (!ReferenceEquals(element, _repeated))
        {
            _repeated = null;
        }

        _ = CheckAsync(new(this, topLevel, element, focus.NavigationMethod), _check.Announcements);
    }

    /// <summary>
    /// Checks the focused control again once a screen reader starts listening: it may have taken focus before the check
    /// could see the bridge, or before the screen reader started.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void OnActivated() => _ = Signal.Return(this, AvaloniaScheduler.Instance).SubscribeSafe(static repair => repair.CheckFocused(), OnError);

    /// <summary>Reports a failure in the subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>
    /// Checks the control that has focus in the active window. How focus arrived is no longer known, so the repeat uses
    /// keyboard navigation and the focus outline stays visible.
    /// </summary>
    private void CheckFocused()
    {
        if (!_check.IsActive)
        {
            return;
        }

        foreach (var window in _windows())
        {
            if (window is not WindowBase { IsActive: true } || window.FocusManager?.GetFocusedElement() is not InputElement element || ReferenceEquals(element, window))
            {
                continue;
            }

            _repeated = null;
            _ = CheckAsync(new(this, window, element, NavigationMethod.Tab), _check.Announcements);
            return;
        }
    }

    /// <summary>Waits for the announcement and repeats the focus change when there was none.</summary>
    /// <param name="change">The focus change.</param>
    /// <param name="announcementsBefore">The announcements counted before it.</param>
    /// <returns>A task that completes once checked.</returns>
    private async Task CheckAsync(FocusChange change, long announcementsBefore)
    {
        // Focus has moved on, so the walk for the previous change is no longer needed. The source is only cancelled,
        // never disposed: it holds no timer or handle, and a walk that is ending must not race a dispose.
        var checking = new CancellationTokenSource();
        if (Interlocked.Exchange(ref _checking, checking) is { } older)
        {
            await older.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            if (await _check.PrepareRepeatAsync(announcementsBefore, checking.Token).ConfigureAwait(false) && !checking.IsCancellationRequested)
            {
                _ = Signal.Return(change, AvaloniaScheduler.Instance).SubscribeSafe(static pending => pending.Repeat(), OnError);
            }
        }
        catch (OperationCanceledException) when (checking.IsCancellationRequested)
        {
            // A newer focus change replaced this check.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A failed check leaves focus as the bridge reported it; the user can still move on.
            OnError(ex);
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

    /// <summary>A focus change waiting for its announcement.</summary>
    /// <param name="Owner">The repair that saw it.</param>
    /// <param name="TopLevel">The window.</param>
    /// <param name="Element">The focused control.</param>
    /// <param name="Method">How focus arrived.</param>
    private sealed record FocusChange(FocusAnnouncementRepair Owner, TopLevel TopLevel, InputElement Element, NavigationMethod Method)
    {
        /// <summary>Repeats the change on the UI thread.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Repeat() => Owner.Repeat(this);
    }
}
