// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>
/// The generated <c>Events()</c> streams of the controls the views name in XAML. The generator cannot see the fields
/// the XAML name generator creates, so each stream is read from a typed receiver here, once, instead of in every view.
/// </summary>
internal static class ControlEventMixins
{
    /// <summary>Adds the click stream of a button.</summary>
    /// <param name="button">The button.</param>
    extension(Button button)
    {
        /// <summary>Gets the clicks.</summary>
        internal IObservable<RoutedEventArgs> Clicks => button.Events().Click;

        /// <summary>Gets the moments the button's flyout opens.</summary>
        internal IObservable<EventArgs> FlyoutOpenings => button.Flyout!.Events().Opened;
    }

    /// <summary>Adds the drop down stream of a combo box.</summary>
    /// <param name="box">The combo box.</param>
    extension(ComboBox box)
    {
        /// <summary>Gets the moments the drop down opens.</summary>
        internal IObservable<EventArgs> DropDownOpenings => box.Events().DropDownOpened;

        /// <summary>
        /// Gets what the user entered each time they pick an item, press Enter or leave the box. A pick gives the item;
        /// the other two give the typed text.
        /// </summary>
        internal IObservable<string?> Entries => Signal.Merge(
            box.SelectionChanges.Select(_ => box.SelectedItem as string),
            box.KeyDowns.Pressing(Key.Enter).Select(_ => box.Text),
            box.LostFocuses.Select(_ => box.Text));
    }

    /// <summary>Adds the descendant streams of a container.</summary>
    /// <param name="container">The container.</param>
    extension(Control container)
    {
        /// <summary>Gets the clicks of every button inside the container, as they stand now.</summary>
        internal IObservable<RoutedEventArgs> DescendantClicks => container.GetLogicalDescendants().ToObservable().OfType<Button>().SelectMany(static button => button.Clicks);
    }

    /// <summary>Adds the keyboard, focus and context streams of an input element.</summary>
    /// <param name="element">The element.</param>
    extension(InputElement element)
    {
        /// <summary>Gets the key presses.</summary>
        internal IObservable<KeyEventArgs> KeyDowns => element.Events().KeyDown;

        /// <summary>Gets the moments the element or a child loses focus.</summary>
        internal IObservable<RoutedEventArgs> LostFocuses => element.Events().LostFocus;

        /// <summary>Gets the moments the element or a child takes focus.</summary>
        internal IObservable<FocusChangedEventArgs> GotFocuses => element.Events().GotFocus;

        /// <summary>Gets the requests for a context menu.</summary>
        internal IObservable<ContextRequestedEventArgs> ContextRequests => element.Events().ContextRequested;

        /// <summary>Gets the pointer presses.</summary>
        internal IObservable<PointerPressedEventArgs> PointerPresses => element.Events().PointerPressed;

        /// <summary>Gets the pointer moves.</summary>
        internal IObservable<PointerEventArgs> PointerMoves => element.Events().PointerMoved;

        /// <summary>Gets the pointer releases.</summary>
        internal IObservable<PointerReleasedEventArgs> PointerReleases => element.Events().PointerReleased;

        /// <summary>Gets the wheel turns.</summary>
        internal IObservable<PointerWheelEventArgs> PointerWheels => element.Events().PointerWheelChanged;

        /// <summary>Gets the taps.</summary>
        internal IObservable<TappedEventArgs> Taps => element.Events().Tapped;

        /// <summary>Gets the double taps.</summary>
        internal IObservable<TappedEventArgs> DoubleTaps => element.Events().DoubleTapped;

        /// <summary>Gets the items picked with a tap or Enter, never by a button inside the item.</summary>
        /// <typeparam name="TItem">The item type.</typeparam>
        /// <returns>The picked items.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<TItem> ItemPicks<TItem>()
            where TItem : class =>
            Signal.Merge<RoutedEventArgs>(element.Taps, element.KeyDowns.Pressing(Key.Enter))
                .Where(static args => !args.IsFromButton)
                .SourceItems<TItem>();

        /// <summary>Gets the items opened with a double tap or Enter, which is marked handled.</summary>
        /// <typeparam name="TItem">The item type.</typeparam>
        /// <returns>The opened items.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<TItem> ItemOpens<TItem>()
            where TItem : class =>
            Signal.Merge<RoutedEventArgs>(element.DoubleTaps, element.KeyDowns.Pressing(Key.Enter).MarkHandled())
                .SourceItems<TItem>();

        /// <summary>Gets the requests for the menu of a list item, which are marked handled.</summary>
        /// <typeparam name="TItem">The item type; requests from controls that show other data are left out.</typeparam>
        /// <returns>The requests.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<ItemContextRequest<TItem>> ItemContextRequests<TItem>()
            where TItem : class =>
            element.ContextRequests
                .Pick(static args => args.Source is Control { DataContext: TItem item } source ? new ItemContextRequest<TItem>(args, source, item) : default(ItemContextRequest<TItem>?))
                .Do(static request => request.Args.Handled = true);
    }

    /// <summary>Adds the container stream of an items control.</summary>
    /// <param name="list">The control.</param>
    extension(ItemsControl list)
    {
        /// <summary>Gets the containers as they are prepared for items.</summary>
        internal IObservable<ContainerPreparedEventArgs> ContainersPrepared => list.Events().ContainerPrepared;

        /// <summary>Gets the containers as they are cleared of their items.</summary>
        internal IObservable<ContainerClearingEventArgs> ContainersClearing => list.Events().ContainerClearing;

        /// <summary>
        /// Gets the pointer presses on the item containers, from the items shown now and every item shown later. A press is
        /// seen here before the list selects the item, which a handler on the list itself would miss when the list handles it.
        /// </summary>
        internal IObservable<PointerPressedEventArgs> ItemPresses => Signal.Merge(
                list.GetRealizedContainers().ToObservable(),
                list.ContainersPrepared.Select(static prepared => prepared.Container))
            .OfType<TemplatedControl>()
            .SelectMany(container => container.TemplatePointerPresses.TakeUntil(list.ContainersClearing.Where(cleared => ReferenceEquals(cleared.Container, container))));
    }

    /// <summary>Adds the click stream of a menu item.</summary>
    /// <param name="item">The menu item.</param>
    extension(MenuItem item)
    {
        /// <summary>Gets the clicks, from the pointer, the keyboard or the item's access key.</summary>
        internal IObservable<RoutedEventArgs> Clicks => item.Events().Click;
    }

    /// <summary>Adds the scroll stream of a scroll bar.</summary>
    /// <param name="bar">The scroll bar.</param>
    extension(ScrollBar bar)
    {
        /// <summary>Gets the scrolls the user makes with the bar.</summary>
        internal IObservable<ScrollEventArgs> Scrolls => bar.Events().Scroll;
    }

    /// <summary>Adds the scroll stream of a scroll viewer.</summary>
    /// <param name="scroller">The scroll viewer.</param>
    extension(ScrollViewer scroller)
    {
        /// <summary>Gets the scroll changes.</summary>
        internal IObservable<ScrollChangedEventArgs> ScrollChanges => scroller.Events().ScrollChanged;
    }

    /// <summary>Adds the selection stream of a list or other selecting control.</summary>
    /// <param name="list">The control.</param>
    extension(SelectingItemsControl list)
    {
        /// <summary>Gets the selection changes.</summary>
        internal IObservable<SelectionChangedEventArgs> SelectionChanges => list.Events().SelectionChanged;
    }

    /// <summary>Adds the template streams of a templated control.</summary>
    /// <param name="control">The control.</param>
    extension(TemplatedControl control)
    {
        /// <summary>Gets the control at the top of the control's template, or <see langword="null"/> before the template is applied.</summary>
        internal Control? TemplateRoot
        {
            get
            {
                using var children = control.GetVisualChildren().GetEnumerator();
                return children.MoveNext() ? children.Current as Control : null;
            }
        }

        /// <summary>Gets the top of the control's template, now and each time the template is applied again.</summary>
        internal IObservable<Control> TemplateRoots => control.Events().TemplateApplied
            .Select(_ => control.TemplateRoot)
            .StartWith(control.TemplateRoot)
            .WhereNotNull();

        /// <summary>
        /// Gets the pointer presses seen by the top of the control's template. A list item handles a left press while
        /// the press bubbles through the item, so a handler on the item never sees it; the template top sees it first.
        /// </summary>
        internal IObservable<PointerPressedEventArgs> TemplatePointerPresses => control.TemplateRoots.SwitchMap(static root => root.PointerPresses);

        /// <summary>
        /// Gets the scrolls of the scroll viewer inside the control's template, including a template applied again. A
        /// template scroll viewer is not the control itself, so its scrolls are not an event of the control.
        /// </summary>
        internal IObservable<ScrollChangedEventArgs> TemplateScrolls => control.Events().TemplateApplied
            .Select(_ => control.FindDescendantOfType<ScrollViewer>())
            .StartWith(control.FindDescendantOfType<ScrollViewer>())
            .WhereNotNull()
            .SwitchMap(static scroller => scroller.ScrollChanges);

        /// <summary>Gets the scrolls the user makes with the scroll bars inside the control's template.</summary>
        internal IObservable<ScrollEventArgs> TemplateBarScrolls => control.Events().TemplateApplied
            .Select(_ => control.GetVisualDescendants().ToObservable().OfType<ScrollBar>())
            .StartWith(control.GetVisualDescendants().ToObservable().OfType<ScrollBar>())
            .SwitchMap(static bars => bars.SelectMany(static bar => bar.Scrolls));
    }

    /// <summary>Adds the text change stream of a text box.</summary>
    /// <param name="box">The text box.</param>
    extension(TextBox box)
    {
        /// <summary>Gets the text changes.</summary>
        internal IObservable<TextChangedEventArgs> TextChanges => box.Events().TextChanged;
    }
}
