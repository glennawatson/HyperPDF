// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>Operators that shape input event streams, so a view reads as one chain and holds no handler methods.</summary>
internal static class EventOperatorMixins
{
    /// <summary>Adds the filters for viewport changes.</summary>
    /// <param name="changes">The viewport changes.</param>
    extension(IObservable<EffectiveViewportChangedEventArgs> changes)
    {
        /// <summary>Keeps the changes that leave part of a control within one viewport height of the visible area.</summary>
        /// <param name="control">The control.</param>
        /// <returns>The matching changes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<EffectiveViewportChangedEventArgs> Nearing(Control control) =>
            changes.WhereWith(
                control,
                static (watched, args) => args.EffectiveViewport.Inflate(new Thickness(0, args.EffectiveViewport.Height)).Intersects(new(watched.Bounds.Size)));
    }

    /// <summary>Adds the key filters.</summary>
    /// <param name="keys">The key presses.</param>
    extension(IObservable<KeyEventArgs> keys)
    {
        /// <summary>Gets the Enter presses that carry no modifier.</summary>
        internal IObservable<KeyEventArgs> Enter => keys.Chord(new(Key.Enter, KeyModifiers.None));

        /// <summary>Gets the Shift+Enter presses.</summary>
        internal IObservable<KeyEventArgs> ShiftEnter => keys.Chord(new(Key.Enter, KeyModifiers.Shift));

        /// <summary>Gets the Escape presses that carry no modifier.</summary>
        internal IObservable<KeyEventArgs> Escape => keys.Chord(new(Key.Escape, KeyModifiers.None));

        /// <summary>Gets the Space presses that carry no modifier.</summary>
        internal IObservable<KeyEventArgs> Space => keys.Chord(new(Key.Space, KeyModifiers.None));

        /// <summary>Gets the Tab presses with or without Shift.</summary>
        internal IObservable<KeyEventArgs> Tab => keys.Pressing(Key.Tab);

        /// <summary>Keeps the presses of one key, whatever modifiers are held.</summary>
        /// <param name="key">The key.</param>
        /// <returns>The matching presses.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<KeyEventArgs> Pressing(Key key) => keys.WhereWith(key, static (wanted, args) => args.Key == wanted);

        /// <summary>Keeps the presses made while all of the given modifiers are held.</summary>
        /// <param name="modifiers">The modifiers that must be held.</param>
        /// <returns>The matching presses.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<KeyEventArgs> Holding(KeyModifiers modifiers) => keys.WhereWith(modifiers, static (held, args) => (args.KeyModifiers & held) == held);

        /// <summary>Keeps the presses made while none of the given modifiers are held.</summary>
        /// <param name="modifiers">The modifiers that must not be held.</param>
        /// <returns>The matching presses.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<KeyEventArgs> Without(KeyModifiers modifiers) => keys.WhereWith(modifiers, static (held, args) => (args.KeyModifiers & held) == 0);

        /// <summary>Keeps the presses of one key with exactly the given modifiers.</summary>
        /// <param name="chord">The key and modifiers.</param>
        /// <returns>The matching presses.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<KeyEventArgs> Chord(KeyChord chord) =>
            keys.WhereWith(chord, static (wanted, args) => args.Key == wanted.Key && args.KeyModifiers == wanted.Modifiers);
    }

    /// <summary>Adds the filters for routed events seen as plain routed arguments.</summary>
    /// <param name="events">The events.</param>
    extension(IObservable<RoutedEventArgs> events)
    {
        /// <summary>Gets the focus losses that leave nothing focused.</summary>
        internal IObservable<FocusChangedEventArgs> FocusDropped => events
            .Select(static args => args as FocusChangedEventArgs)
            .KeepNotNull()
            .Where(static args => args.NewFocusedElement is null);

        /// <summary>Gets the data of the control each event came from, for the events whose control shows an item of the given type.</summary>
        /// <typeparam name="TItem">The item type.</typeparam>
        /// <returns>The items.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<TItem> SourceItems<TItem>()
            where TItem : class =>
            events.Select(static args => (args.Source as StyledElement)?.DataContext as TItem).KeepNotNull();
    }

    /// <summary>Adds the operators every stream can use.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The stream.</param>
    extension<T>(IObservable<T> source)
    {
        /// <summary>Gets the stream as plain signals, the input a command without a parameter takes.</summary>
        internal IObservable<RxVoid> AsVoid => source.Select(static _ => RxVoid.Default);

        /// <summary>Pairs each value with the latest value of a state stream; values wait until the state has one.</summary>
        /// <typeparam name="TState">The state type.</typeparam>
        /// <param name="state">The state, for example the view model the view shows.</param>
        /// <returns>Each value with its state.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<Injected<T, TState>> Inject<TState>(IObservable<TState?> state)
            where TState : class =>
            source.WithLatestFrom(state.WhereNotNull(), static (value, latest) => new Injected<T, TState>(value, latest));

        /// <summary>Replaces each value with the latest value of a state stream, and waits until the state has one.</summary>
        /// <typeparam name="TState">The state type.</typeparam>
        /// <param name="state">The state, for example the view model the view shows.</param>
        /// <returns>The latest state, once per value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<TState> InjectLatest<TState>(IObservable<TState?> state)
            where TState : class =>
            source.WithLatestFrom(state, static (_, latest) => latest).WhereNotNull();

        /// <summary>Gets the rows a tree shows for the values, for the values it shows now.</summary>
        /// <param name="tree">The tree.</param>
        /// <returns>The rows.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<TreeViewItem> RowsIn(TreeView tree) =>
            source.SelectWith(tree, static (shown, item) => item is null ? null : shown.ContainerFromItem(item) as TreeViewItem).KeepNotNull();

        /// <summary>Gets the rows a list shows for the values, for the values it shows now.</summary>
        /// <param name="list">The list.</param>
        /// <returns>The rows.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<ListBoxItem> RowsIn(ListBox list) =>
            source.SelectWith(list, static (shown, item) => item is null ? null : shown.ContainerFromItem(item) as ListBoxItem).KeepNotNull();

        /// <summary>Keeps the values seen while the keyboard focus is outside an area, for example after the control that held it was replaced.</summary>
        /// <param name="area">The area.</param>
        /// <returns>The values seen while the focus is elsewhere.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<T> WhileFocusOutside(Visual area) =>
            source.WhereWith(area, static (watched, _) => !(TopLevel.GetTopLevel(watched)?.FocusManager?.GetFocusedElement() is Visual focused && watched.IsVisualAncestorOf(focused)));

        /// <summary>
        /// Keeps the values seen while the keyboard focus is inside an area or on a control that is no longer shown, which is the
        /// case when the area is about to be hidden, so the focus can be moved before it is lost.
        /// </summary>
        /// <param name="area">The area.</param>
        /// <returns>The values seen while the area holds the focus.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<T> WhileFocusInside(Visual area) =>
            source.WhereWith(
                area,
                static (watched, _) => TopLevel.GetTopLevel(watched)?.FocusManager?.GetFocusedElement() is Visual focused
                    && (watched.IsVisualAncestorOf(focused) || focused is InputElement { IsEffectivelyVisible: false }));

        /// <summary>
        /// Runs an async handler for each value, and cancels the handler still running for the previous value when the
        /// next one arrives, so a slow older request can never finish after and override a newer one.
        /// </summary>
        /// <param name="handler">The handler; it should stop when its token is cancelled.</param>
        /// <returns>The handlers' results.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<RxVoid> SwitchAsync(Func<T, CancellationToken, Task<RxVoid>> handler) =>
            source.SwitchMap(value => Signal.FromAsync(token => handler(value, token)));

        /// <summary>Turns each value into a result when it has one, and leaves a gap in the stream when it has none.</summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="selector">Gives the result, or <see langword="null"/> when the value has none.</param>
        /// <returns>The results that exist.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<TResult> Pick<TResult>(Func<T, TResult?> selector)
            where TResult : struct =>
            source.Select(selector).KeepValues;
    }

    /// <summary>Adds the operators every routed event stream shares.</summary>
    /// <typeparam name="TArgs">The event arguments.</typeparam>
    /// <param name="events">The events.</param>
    extension<TArgs>(IObservable<TArgs> events)
        where TArgs : RoutedEventArgs
    {
        /// <summary>Marks each event handled as it passes, so no other control acts on it.</summary>
        /// <returns>The same events.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<TArgs> MarkHandled() => events.Do(static args => args.Handled = true);
    }

    /// <summary>Adds the unwrapping of optional values.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="optional">The optional values.</param>
    extension<TValue>(IObservable<TValue?> optional)
        where TValue : struct
    {
        /// <summary>Gets the values that are present, so a step that has nothing to give can leave a gap in the stream.</summary>
        internal IObservable<TValue> KeepValues => optional.Where(static value => value.HasValue).Select(static value => value.GetValueOrDefault());
    }

    /// <summary>Adds the source test of a routed event.</summary>
    /// <param name="args">The event.</param>
    extension(RoutedEventArgs args)
    {
        /// <summary>Gets a value indicating whether the event came from a button, or from inside one.</summary>
        internal bool IsFromButton => args.Source is Visual source && (source is Button || source.FindAncestorOfType<Button>() is not null);
    }
}
