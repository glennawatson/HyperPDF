// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Layout;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>A document tab: its tool bars, sidebar and pages. Every binding is made here with ReactiveUI.Binding.</summary>
[DebuggerDisplay("DocumentView: {ViewModel}")]
public sealed partial class DocumentView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>The smallest editor font size.</summary>
    private const double MinFieldFontSize = 10;

    /// <summary>The editor font size as a share of the field height.</summary>
    private const double FieldFontShare = 0.6;

    /// <summary>Halves the room around a comb letter, to centre it in its box.</summary>
    private const double HalfCell = 0.5;

    /// <summary>Opaque alpha in 0xAARRGGBB.</summary>
    private const uint OpaqueAlpha = 0xFF000000U;

    /// <summary>The misspelled words in the field being edited, reused between checks.</summary>
    private readonly List<Core.Reading.TextRange> _misspelled = [];

    /// <summary>Initializes a new instance of the <see cref="DocumentView"/> class.</summary>
    public DocumentView()
    {
        InitializeComponent();
        FieldLabels.Link((OpenCertificatePathBox, OpenCertificateLabel), (OpenCertificatePasswordBox, OpenCertificatePasswordLabel));
        FieldSpelling.Editor = FieldEditor;
        PageTextUnderline.Editor = PageTextEditor;
        ThumbnailList.ItemTemplate = new FuncDataTemplate<ThumbnailItemViewModel>((_, _) => new ThumbnailItemView { Tab = ViewModel });
        OutlineTree.ItemTemplate = new FuncTreeDataTemplate<OutlineItemViewModel>(static (_, _) => new OutlineItemView(), static item => item.Children);
        SearchResultList.ItemTemplate = new FuncDataTemplate<SearchResultItemViewModel>(static (_, _) => new SearchResultView());
        AnnotationList.ItemTemplate = new FuncDataTemplate<AnnotationItemViewModel>(static (_, _) => new AnnotationItemView());
        AttachmentList.ItemTemplate = new FuncDataTemplate<DocumentAttachment>(static (_, _) => new AttachmentItemView());
        LayerList.ItemTemplate = new FuncDataTemplate<LayerItemViewModel>(static (_, _) => new LayerItemView());
        VoiceBox.ItemTemplate = new FuncDataTemplate<string>(static (text, _) => new TextBlock { Text = text });
        SpeedBox.ItemTemplate = new FuncDataTemplate<string>(static (text, _) => new TextBlock { Text = text });
        LanguageBox.ItemTemplate = new FuncDataTemplate<string>(static (text, _) => new TextBlock { Text = text });
        LanguageBox.ItemsSource = TextRecognitionViewModel.LanguageNames;
        SingleLayoutItem.CommandParameter = "Single";
        DualLayoutItem.CommandParameter = "Dual";
        CoverLayoutItem.CommandParameter = "DualCover";
        FieldLabels.Link((ScaleBox, ScaleLabel));
        SetUpAnnotationList();
        SetUpTextFormat();
        _ = this.WhenActivated(disposables =>
        {
            BindToolBar(disposables);
            BindAnnotationTools(disposables);
            BindAnnotationEditing(disposables);
            BindBars(disposables);
            BindFind(disposables);
            BindSidebar(disposables);
            BindPages(disposables);
            BindWindowCommands(disposables);
            BindFieldEditor(disposables);
            BindTextFormat(disposables);
            BindPageTextEditor(disposables);
            disposables.Add(OnMainThread().SubscribeSafe(_ => FocusCanvas(), OnError));
        });
    }

    /// <summary>Focuses the page box.</summary>
    public void FocusPageBox()
    {
        _ = PageBox.Focus();
        PageBox.SelectAll();
    }

    /// <summary>Copies the selected text.</summary>
    /// <returns>The selected text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetSelectedText() => Canvas.GetSelectedText();

    /// <summary>Focuses the main content: the Focus Mode text when it is on, otherwise the pages.</summary>
    internal void FocusMain()
    {
        if (ViewModel?.FocusMode.IsOn == true)
        {
            FocusPane.FocusFirstControl();
            return;
        }

        FocusCanvas();
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>
    /// Keeps a handler registered on the interaction of whichever tab the view shows. This view is reused across tabs,
    /// and <c>BindInteraction</c> stays on the view model it was given rather than following <c>ViewModel</c>.
    /// </summary>
    /// <typeparam name="TInput">The interaction input.</typeparam>
    /// <typeparam name="TOutput">The interaction output.</typeparam>
    /// <param name="interactions">The shown tab's interaction, from <c>WhenChanged</c>.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The registration.</returns>
    private static MultipleDisposable HandleInteraction<TInput, TOutput>(
        IObservable<IInteraction<TInput, TOutput>?> interactions,
        Func<IInteractionContext<TInput, TOutput>, Task> handler)
    {
        var registration = new SwapDisposable();
        var follow = interactions.SubscribeSafe(interaction => registration.Disposable = interaction?.RegisterHandler(handler), OnError);
        return new(follow, registration);
    }

    /// <summary>Creates a signal that fires once, later on the main thread, after the current UI work.</summary>
    /// <returns>The signal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<RxVoid> OnMainThread() => Signal.Return(RxVoid.Default).ObserveOn(RxSchedulers.MainThreadScheduler);

    /// <summary>Creates a fixed command parameter for a menu item or button.</summary>
    /// <typeparam name="T">The parameter type.</typeparam>
    /// <param name="value">The parameter.</param>
    /// <returns>A signal that gives the parameter and stays open.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<T> Parameter<T>(T value)
    {
        // The command binder keeps CommandParameter only while this signal is open; after a plain Signal.Return
        // completes, the control passes null to the command.
        return Signal.Concat(Signal.Return(value), Signal.Never<T>());
    }

    /// <summary>Converts a nullable toggle state to a plain flag.</summary>
    /// <param name="value">The toggle state.</param>
    /// <returns><see langword="true"/> only when checked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOn(bool? value) => value == true;

    /// <summary>Gets the scroll bar visibility: hidden while presenting, so only the page shows.</summary>
    /// <param name="presenting">Whether the tab is presenting.</param>
    /// <returns>The visibility.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ScrollBarVisibility ScrollBars(bool presenting) => presenting ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;

    /// <summary>Describes how many signatures a document has.</summary>
    /// <param name="count">The number of signatures.</param>
    /// <returns>The sentence.</returns>
    private static string DescribeSignatures(int count) =>
        count == 1 ? "This document is digitally signed." : string.Create(CultureInfo.CurrentCulture, $"This document has {count} digital signatures.");

    /// <summary>Creates a stream of Enter presses, with no modifier, in a control.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The presses.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<RxVoid> EnterPresses(Control control) =>
        control.Events().KeyDown.Where(static args => args.Key == Key.Enter && args.KeyModifiers == KeyModifiers.None).Select(static _ => RxVoid.Default);

    /// <summary>Creates a stream of Shift+Enter presses in a control.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The presses.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<RxVoid> ShiftEnterPresses(Control control) =>
        control.Events().KeyDown.Where(static args => args.Key == Key.Enter && args.KeyModifiers == KeyModifiers.Shift).Select(static _ => RxVoid.Default);

    /// <summary>Creates a stream of Escape presses, with no modifier, in a control.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The presses.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<RxVoid> EscapePresses(Control control) =>
        control.Events().KeyDown.Where(static args => args.Key == Key.Escape && args.KeyModifiers == KeyModifiers.None).Select(static _ => RxVoid.Default);

    /// <summary>Creates a stream of the selections made in a list box.</summary>
    /// <param name="list">The list box.</param>
    /// <returns>The selection changes; the event source is the list box.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<SelectionChangedEventArgs> Selections(ListBox list) => list.Events().SelectionChanged;

    /// <summary>Creates a stream of the context menu requests in a control.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The requests.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<ContextRequestedEventArgs> ContextRequests(Control control) => control.Events().ContextRequested;

    /// <summary>Creates a stream that fires when a control loses focus.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The events.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<RoutedEventArgs> FocusLosses(Control control) => control.Events().LostFocus;

    /// <summary>Describes a field and the possible spelling mistakes in it, for screen readers.</summary>
    /// <param name="text">The field's text.</param>
    /// <param name="misspelled">The misspelled words.</param>
    /// <returns>The description.</returns>
    private static string DescribeSpelling(string text, List<Core.Reading.TextRange> misspelled)
    {
        if (misspelled.Count == 0)
        {
            return Descriptions.FormField;
        }

        var words = new List<string>(misspelled.Count);
        foreach (var range in misspelled)
        {
            words.Add(text.Substring(range.Start, range.Length));
        }

        var count = misspelled.Count == 1 ? "1 possible spelling mistake" : string.Create(CultureInfo.CurrentCulture, $"{misspelled.Count} possible spelling mistakes");
        return string.Create(CultureInfo.CurrentCulture, $"{count}: {string.Join(", ", words)}. Open the menu on a word for corrections. {Descriptions.FormField}");
    }

    /// <summary>Determines whether a word is one of the marked words.</summary>
    /// <param name="misspelled">The marked words.</param>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> when marked.</returns>
    private static bool Contains(List<Core.Reading.TextRange> misspelled, Core.Reading.TextRange word)
    {
        foreach (var range in misspelled)
        {
            if (range == word)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Moves focus to the pages when a part of the view was hidden while it held the focus, so focus is never lost.</summary>
    /// <param name="hidden">The part that was hidden.</param>
    private void KeepFocus(Control hidden)
    {
        // Focus elsewhere, for example on the tab strip after switching tabs, is left where it is.
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused
            && (hidden.IsVisualAncestorOf(focused) || focused is InputElement { IsEffectivelyVisible: false }))
        {
            FocusCanvas();
        }
    }

    /// <summary>Focuses the page canvas.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FocusCanvas() => _ = Canvas.Focus();

    /// <summary>Focuses the find box.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FocusSearchBox() => _ = SearchBox.Focus();

    /// <summary>Focuses the form field editor and selects its text.</summary>
    private void FocusFieldEditor()
    {
        _ = FieldEditor.Focus();
        FieldEditor.SelectAll();
    }

    /// <summary>Binds the main tool bar.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindToolBar(MultipleDisposable bindings)
    {
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SaveCommand, static v => v.SaveButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.HasUnsavedChanges, static v => v.SaveButton.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsAnnotating, static v => v.AnnotateToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.FillAndSign.IsActive, static v => v.FillSignToggle.IsChecked, static on => on));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.ToggleCommand, static v => v.FillSignToggle));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.CanAnnotate, static v => v.AnnotateToggle.IsEnabled));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.CanAnnotate, static v => v.FillSignToggle.IsEnabled));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Search.IsOpen, static v => v.FindToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.RotateLeftCommand, static v => v.RotateLeftItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.RotateRightCommand, static v => v.RotateRightItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetLayoutCommand, static v => v.SingleLayoutItem, Parameter("Single")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetLayoutCommand, static v => v.DualLayoutItem, Parameter("Dual")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetLayoutCommand, static v => v.CoverLayoutItem, Parameter("DualCover")));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.LayoutMode, static v => v.SingleLayoutItem.IsChecked, static mode => mode == PageLayoutMode.Single));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.LayoutMode, static v => v.DualLayoutItem.IsChecked, static mode => mode == PageLayoutMode.Dual));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.LayoutMode, static v => v.CoverLayoutItem.IsChecked, static mode => mode == PageLayoutMode.DualCover));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SaveCommand, static v => v.SaveItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.PrintCommand, static v => v.PrintItem));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.PrintPreviewInteraction), ShowPrintPreviewAsync));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.PresentCommand, static v => v.PresentItem));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ShowsChrome, static v => v.Chrome.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsReading, static v => v.ReadModeBar.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.ReadModeTitle.Text));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ReadModeCommand, static v => v.LeaveReadModeButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ReadModeCommand, static v => v.ReadModeItem));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsPresenting, static v => v.Scroller.VerticalScrollBarVisibility, ScrollBars));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsPresenting, static v => v.Scroller.HorizontalScrollBarVisibility, ScrollBars));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsCaretMode, static v => v.CaretModeItem.IsChecked, static on => on, static on => on));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsPageByPage, static v => v.PageByPageItem.IsChecked, static on => on, static on => on));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsSplitView, static v => v.SplitViewItem.IsChecked));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SplitViewCommand, static v => v.SplitViewItem));
        BindPageTools(bindings);
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SaveAsCommand, static v => v.SaveAsItem));
        BindRedaction(bindings);
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ReloadCommand, static v => v.ReloadItem));
        bindings.Add(this.Bind(ViewModel, static vm => vm.SidebarVisible, static v => v.SidebarToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.GoBackCommand, static v => v.BackButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.GoForwardCommand, static v => v.ForwardButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.PreviousPageCommand, static v => v.PreviousPageButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.NextPageCommand, static v => v.NextPageButton));
        bindings.Add(this.Bind(ViewModel, static vm => vm.PageEntry, static v => v.PageBox.Text, static entry => entry, static text => text ?? string.Empty));
        bindings.Add(EnterPresses(PageBox).InvokeCommand(this, static v => v.ViewModel!.GoToPageEntryCommand));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.PageCount, static v => v.PageCountText.Text, static count => string.Create(CultureInfo.CurrentCulture, $"of {count}")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ZoomOutCommand, static v => v.ZoomOutButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ZoomInCommand, static v => v.ZoomInButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ZoomText, static v => v.ZoomButton.Content));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FitWidthCommand, static v => v.FitWidthItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FitPageCommand, static v => v.FitPageItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom50Item, Parameter("50")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom75Item, Parameter("75")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom100Item, Parameter("100")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom125Item, Parameter("125")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom150Item, Parameter("150")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom200Item, Parameter("200")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom400Item, Parameter("400")));
    }

    /// <summary>Binds moving around and grabbing content: the drag tools, auto-scroll, the first and last page and select all.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindPageTools(MultipleDisposable bindings)
    {
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetPageToolCommand, static v => v.SelectTextToolItem, Parameter(PageTool.SelectText)));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetPageToolCommand, static v => v.HandToolItem, Parameter(PageTool.Hand)));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetPageToolCommand, static v => v.ZoomAreaToolItem, Parameter(PageTool.ZoomArea)));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ToggleSnapshotToolCommand, static v => v.SnapshotToolItem));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsSelectTextTool, static v => v.SelectTextToolItem.IsChecked));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsHandTool, static v => v.HandToolItem.IsChecked));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsZoomAreaTool, static v => v.ZoomAreaToolItem.IsChecked));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsSnapshotTool, static v => v.SnapshotToolItem.IsChecked));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsAutoScrolling, static v => v.AutoScrollItem.IsChecked, static on => on, static on => on));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsAutoScrolling, static v => v.AutoScrollBar.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.AutoScrollText, static v => v.AutoScrollText.Text));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SlowerAutoScrollCommand, static v => v.AutoScrollSlowerButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FasterAutoScrollCommand, static v => v.AutoScrollFasterButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.StopAutoScrollCommand, static v => v.AutoScrollStopButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FirstPageCommand, static v => v.FirstPageItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.LastPageCommand, static v => v.LastPageItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SelectAllCommand, static v => v.SelectAllItem));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.SaveImageInteraction), SaveImageAsync));
    }

    /// <summary>Binds the Annotate and Fill &amp; Sign tool rows.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindAnnotationTools(MultipleDisposable bindings)
    {
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.IsAnnotating, static v => v.AnnotateBar.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Measure.IsOn, static v => v.MeasureBar.IsVisible));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Measure.ToggleCommand, static v => v.MeasureItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Measure.KeepCommand, static v => v.MeasureKeepButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Measure.CanKeep, static v => v.MeasureKeepButton.IsEnabled));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Measure.ClearCommand, static v => v.MeasureClearButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Measure.DoneCommand, static v => v.MeasureDoneButton));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Measure.IsDistance, static v => v.DistanceTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Measure.IsPerimeter, static v => v.PerimeterTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Measure.IsArea, static v => v.AreaTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Measure.ScaleText, static v => v.ScaleBox.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Measure.Result, static v => v.MeasureResult.Text));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.UndoCommand, static v => v.UndoButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.CanUndo, static v => v.UndoButton.IsEnabled));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.DoneCommand, static v => v.AnnotateDoneButton));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsSelectTool, static v => v.SelectTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsHighlightTool, static v => v.HighlightTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsUnderlineTool, static v => v.UnderlineTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsStrikeOutTool, static v => v.StrikeOutTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsDrawTool, static v => v.DrawTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsNoteTool, static v => v.NoteTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsTextTool, static v => v.TextTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.ShapeLabel, static v => v.ShapeText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.StampButtonLabel, static v => v.StampText.Text));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.RectangleItem, Parameter(AnnotationTool.Rectangle)));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.EllipseItem, Parameter(AnnotationTool.Ellipse)));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.ArrowItem, Parameter(AnnotationTool.Arrow)));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetToolCommand, static v => v.LineItem, Parameter(AnnotationTool.Line)));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetStampCommand, static v => v.ApprovedItem, Parameter("APPROVED")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetStampCommand, static v => v.ReviewedItem, Parameter("REVIEWED")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetStampCommand, static v => v.DraftItem, Parameter("DRAFT")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetStampCommand, static v => v.ConfidentialItem, Parameter("CONFIDENTIAL")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetStampCommand, static v => v.FinalItem, Parameter("FINAL")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetStampCommand, static v => v.NotApprovedItem, Parameter("NOT APPROVED")));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.ColorName, static v => v.ColourText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.Color, static v => v.ColourSwatch.Background, static color => new SolidColorBrush(Color.FromUInt32(OpaqueAlpha | color))));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.YellowItem, Parameter("Yellow")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.GreenItem, Parameter("Green")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.BlueItem, Parameter("Blue")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.RedItem, Parameter("Red")));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.Annotations.PromptInteraction), PromptAsync));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.SaveAsInteraction), SaveAsAsync));
        BindFillAndSign(bindings);
    }

    /// <summary>Binds the Fill &amp; Sign tools: making, placing and adjusting a signature, and certificate signing.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindFillAndSign(MultipleDisposable bindings)
    {
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.FillAndSign.IsActive, static v => v.FillSignBar.IsVisible));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.DoneCommand, static v => v.FillSignDoneButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.SignatureCommand, static v => v.SignatureButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.InitialsCommand, static v => v.InitialsButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.PlaceCommand, static v => v.PlaceMarkButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.BiggerCommand, static v => v.BiggerMarkButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.SmallerCommand, static v => v.SmallerMarkButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.CancelPlacementCommand, static v => v.CancelPlacementButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.AdjustCommand, static v => v.AdjustMarkButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.RemoveMarkCommand, static v => v.RemoveMarkButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.FillAndSign.Placement, static v => v.PlacingTools.IsVisible, static placement => placement is not null));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.FillAndSign.HasPlacedMark, static v => v.PlacedTools.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.FillAndSign.Hint, static v => v.FillSignHint.Text));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.FillAndSign.MarkInteraction), ShowSignatureMarkAsync));

        // Escape cancels placing wherever the keyboard is, for example on the Place button.
        bindings.Add(EscapePresses(this).InvokeCommand(this, static v => v.ViewModel!.FillAndSign.CancelPlacementCommand));

        // Arrow keys move the mark being placed, so the pages take the keyboard as soon as placing starts.
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.FillAndSign.Placement)
            .Select(static placement => placement is not null)
            .DistinctUntilChanged()
            .Where(static placing => placing)

            // After the signature window has closed and handed focus back.
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(_ => FocusCanvas(), OnError));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Signatures.SignWithCertificateCommand, static v => v.CertificateSignButton));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.Signatures.CertificateSignInteraction), ShowCertificateSignAsync));
    }

    /// <summary>Binds the signed, notice and reload bars.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindBars(MultipleDisposable bindings)
    {
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Signatures.SignatureCount, static v => v.SignedBar.IsVisible, static count => count > 0));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Signatures.SignatureCount, static v => v.SignedText.Text, DescribeSignatures));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Signatures.CheckCommand, static v => v.CheckSignaturesButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Signatures.IsChecking, static v => v.CheckSignaturesButton.Content, static checking => checking ? "Checking…" : "Check Signatures"));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.Signatures.ShowInteraction), ShowSignaturesAsync));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Notice, static v => v.NoticeText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Notice, static v => v.NoticeBar.IsVisible, static notice => notice is not null));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.DismissNoticeCommand, static v => v.DismissNoticeButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.TextRecognition.RecognizeCommand, static v => v.RecognizeTextItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.TextRecognition.StopCommand, static v => v.StopRecognitionButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.IsRunning, static v => v.RecognitionBar.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.ProgressText, static v => v.RecognitionText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.Progress, static v => v.RecognitionProgress.Value));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.TextRecognition.ChooseLanguageCommand, static v => v.RecognizeLanguageItem));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.IsLanguageBarOpen, static v => v.LanguageBar.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.LanguageBarText, static v => v.LanguageBarText.Text));
        bindings.Add(this.Bind(ViewModel, static vm => vm.TextRecognition.LanguageIndex, static v => v.LanguageBox.SelectedIndex));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.LanguageActionText, static v => v.LanguageActionButton.Content));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.IsDownloading, static v => v.LanguageDownloadProgress.Opacity, static downloading => downloading ? 1D : 0D));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.DownloadProgress, static v => v.LanguageDownloadProgress.Value));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.TextRecognition.ConfirmLanguageCommand, static v => v.LanguageActionButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.TextRecognition.CloseLanguageBarCommand, static v => v.CloseLanguageBarButton));
        BindReadAloud(bindings);
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.HasPendingReload, static v => v.ReloadBar.IsVisible));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ReloadCommand, static v => v.ReloadButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.DismissReloadCommand, static v => v.DismissReloadButton));
    }

    /// <summary>Binds the Read Aloud button and bar.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindReadAloud(MultipleDisposable bindings)
    {
        bindings.Add(this.Bind(ViewModel, static vm => vm.FocusMode.IsOn, static v => v.FocusToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.FocusMode, static v => v.FocusPane.ViewModel));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.FocusMode.IsOn, static v => v.FocusPane.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.FocusMode.IsOn, static v => v.Scroller.IsVisible, static on => !on));
        bindings.Add(this.Bind(ViewModel, static vm => vm.ReadAloud.IsOpen, static v => v.ReadAloudToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.IsOpen, static v => v.ReadAloudBar.IsVisible));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ReadAloud.CloseCommand, static v => v.CloseReadAloudButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ReadAloud.PlayPauseCommand, static v => v.PlayButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ReadAloud.PlayPauseCommand, static v => v.PauseButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ReadAloud.PreviousCommand, static v => v.PreviousSentenceButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ReadAloud.NextCommand, static v => v.NextSentenceButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.IsPlaying, static v => v.PauseButton.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.IsPlaying, static v => v.PlayButton.IsVisible, static playing => !playing));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.VoiceNames, static v => v.VoiceBox.ItemsSource));
        bindings.Add(this.Bind(ViewModel, static vm => vm.ReadAloud.VoiceIndex, static v => v.VoiceBox.SelectedIndex));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.SpeedNames, static v => v.SpeedBox.ItemsSource));
        bindings.Add(this.Bind(ViewModel, static vm => vm.ReadAloud.SpeedIndex, static v => v.SpeedBox.SelectedIndex));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.CanDownloadVoice, static v => v.DownloadVoiceButton.IsVisible));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ReadAloud.DownloadVoiceCommand, static v => v.DownloadVoiceButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.IsDownloading, static v => v.VoiceDownloadProgress.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.DownloadProgress, static v => v.VoiceDownloadProgress.Value));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ReadAloud.StatusText, static v => v.ReadAloudText.Text));
        bindings.Add(this.Bind(ViewModel, static vm => vm.FocusMode.WordHighlight, static v => v.WordMarkCheck.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.FocusMode.FocusBand, static v => v.FocusBandCheck.IsChecked, static on => on, IsOn));
    }

    /// <summary>Binds the find bar.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindFind(MultipleDisposable bindings)
    {
        // Registered before the find bar's visibility binding, so the focus is checked before the bar hides.
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.Search.IsOpen).Where(static open => !open).SubscribeSafe(_ => KeepFocus(FindBar), OnError));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Search.IsOpen, static v => v.FindBar.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Search.Query, static v => v.SearchBox.Text, static query => query, static text => text ?? string.Empty));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Search.MatchCase, static v => v.MatchCaseToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Search.WholeWord, static v => v.WholeWordToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Search.PreviousCommand, static v => v.PreviousResultButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Search.NextCommand, static v => v.NextResultButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Search.CloseCommand, static v => v.CloseFindButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Search.Status, static v => v.SearchStatusText.Text));
        bindings.Add(EnterPresses(SearchBox).InvokeCommand(this, static v => v.ViewModel!.Search.NextCommand));
        bindings.Add(ShiftEnterPresses(SearchBox).InvokeCommand(this, static v => v.ViewModel!.Search.PreviousCommand));
        bindings.Add(EscapePresses(SearchBox).InvokeCommand(this, static v => v.ViewModel!.Search.CloseCommand));
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.Search.IsOpen)
            .Where(static open => open)
            .SelectMany(static _ => OnMainThread())
            .SubscribeSafe(_ => FocusSearchBox(), OnError));
    }

    /// <summary>Binds the sidebar panels.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindSidebar(MultipleDisposable bindings)
    {
        // Items are named as their containers are prepared, so these come before the lists are filled.
        bindings.Add(ItemAutomation.NameItems(ThumbnailList));
        bindings.Add(ItemAutomation.NameItems(SearchResultList));
        bindings.Add(ItemAutomation.NameItems(AnnotationList));
        bindings.Add(ItemAutomation.NameItems(AttachmentList));
        bindings.Add(ItemAutomation.NameItems(LayerList));

        // Registered before the sidebar's visibility binding, so the focus is checked before the sidebar hides.
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.SidebarVisible).Where(static visible => !visible).SubscribeSafe(_ => KeepFocus(Sidebar), OnError));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.SidebarVisible, static v => v.Sidebar.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.SidebarVisible, static v => v.SidebarSplitter.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsThumbnailsMode, static v => v.ThumbnailsToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsOutlineMode, static v => v.OutlineToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsSearchMode, static v => v.SearchResultsToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsAnnotationsMode, static v => v.AnnotationsToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsThumbnailsMode, static v => v.ThumbnailPanel.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Pages, static v => v.PageTools.ViewModel));
        bindings.Add(new ThumbnailPageInteraction(this, ThumbnailList));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Pages.SelectedThumbnails, static v => v.ThumbnailList.SelectedItems));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Thumbnails, static v => v.ThumbnailList.ItemsSource));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ReduceMotion, static v => v.ThumbnailList.ReduceMotion));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsOutlineMode, static v => v.OutlinePanel.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Outline, static v => v.OutlineTree.ItemsSource));
        bindings.Add(this.Bind(ViewModel, static vm => vm.SelectedOutlineItem, static v => v.OutlineTree.SelectedItem, static item => item, static item => item as OutlineItemViewModel));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.HasOutline, static v => v.NoOutlineText.IsVisible, static has => !has));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsAnnotationsMode, static v => v.AnnotationsPanel.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.VisibleItems, static v => v.AnnotationList.ItemsSource));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.Items.Count, static v => v.NoAnnotationsText.IsVisible, static count => count == 0));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Layers.HasLayers, static v => v.LayersToggle.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsLayersMode, static v => v.LayersToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsLayersMode, static v => v.LayersPanel.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Layers.Items, static v => v.LayerList.ItemsSource));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Attachments.HasAttachments, static v => v.AttachmentsToggle.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsAttachmentsMode, static v => v.AttachmentsToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsAttachmentsMode, static v => v.AttachmentsPanel.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Attachments.Items, static v => v.AttachmentList.ItemsSource));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Attachments.Selected, static v => v.AttachmentList.SelectedItem, static item => item, static item => item as DocumentAttachment));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Attachments.SaveCommand, static v => v.SaveAttachmentButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Attachments.OpenSelectedCommand, static v => v.OpenAttachmentButton));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.Attachments.SaveInteraction), SaveAttachmentAsync));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsSearchMode, static v => v.SearchResultList.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Search.Results, static v => v.SearchResultList.ItemsSource));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Search.SelectedResult, static v => v.SearchResultList.SelectedItem, static item => item, static item => item as SearchResultItemViewModel));

        // Events() cannot see the controls the XAML name generator declares, so the streams come from typed parameters.
        bindings.Add(Selections(AnnotationList)
            .Select(static args => (args.Source as ListBox)?.SelectedItem as AnnotationItemViewModel)
            .Where(static item => item is not null)
            .InvokeCommand(this, static v => v.ViewModel!.Annotations.GoToCommand));

        bindings.Add(ContextRequests(AnnotationList).SubscribeSafe(OnAnnotationContextRequested, OnError));
        bindings.Add(ThumbnailList.ObserveRouted(ScrollViewer.ScrollChangedEvent, RoutingStrategies.Bubble, handledEventsToo: true)
            .Where(static args => args.OffsetDelta.Y != 0)
            .SubscribeSafe(_ => OnThumbnailsScrolled(), OnError));
    }

    /// <summary>Binds the page area, the error panel and the password panel.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindPages(MultipleDisposable bindings)
    {
        bindings.Add(this.WhenChanged(static v => v.ViewModel).BindTo(this, static v => v.Canvas.Tab));
        bindings.Add(PageArea.ObserveRouted(InputElement.PointerPressedEvent, RoutingStrategies.Tunnel).SubscribeSafe(StopAutoScrollOnPagePress, OnError));
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.UriRequests).SwitchTo().SubscribeSafe(OpenUri, OnError));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ContentWarning, static v => v.ContentWarningText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ContentWarning, static v => v.ContentWarningBar.IsVisible, static warning => warning is not null));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.DismissContentWarningCommand, static v => v.DismissContentWarningButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.RepairNotice, static v => v.RepairNoticeText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.RepairNotice, static v => v.RepairNoticeBar.IsVisible, static notice => notice is not null));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.RepairDetails, static v => v.RepairDetailsText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.RepairDetails, static v => v.RepairDetailsText.IsVisible, static details => details is not null));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.RepairDetails, static v => v.ToggleRepairDetailsButton.Content, static details => details is null ? "Details" : "Hide details"));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ToggleRepairDetailsCommand, static v => v.ToggleRepairDetailsButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.DismissRepairNoticeCommand, static v => v.DismissRepairNoticeButton));
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.FileLaunchRequests).SwitchMap(static requests => requests).SubscribeSafe(LaunchFile, OnError));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.ConfirmOpenFileInteraction), ConfirmOpenFileAsync));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ErrorMessage, static v => v.ErrorText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ErrorMessage, static v => v.ErrorPanel.IsVisible, static message => message is not null));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.NeedsPassword, static v => v.PasswordPanel.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.PasswordEntry, static v => v.PasswordBox.Text, static entry => entry, static text => text ?? string.Empty));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SubmitPasswordCommand, static v => v.UnlockButton));
        bindings.Add(EnterPresses(PasswordBox).InvokeCommand(this, static v => v.ViewModel!.SubmitPasswordCommand));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.NeedsCertificate, static v => v.CertificatePanel.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.CertificatePathEntry, static v => v.OpenCertificatePathBox.Text, static entry => entry, static text => text ?? string.Empty));
        bindings.Add(this.Bind(ViewModel, static vm => vm.CertificatePasswordEntry, static v => v.OpenCertificatePasswordBox.Text, static entry => entry, static text => text ?? string.Empty));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.CertificateError, static v => v.OpenCertificateErrorText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.CertificateError, static v => v.OpenCertificateErrorText.IsVisible, static error => error is not null));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.BrowseCertificateCommand, static v => v.BrowseOpenCertificateButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SubmitCertificateCommand, static v => v.UnlockCertificateButton));
        bindings.Add(EnterPresses(OpenCertificatePasswordBox).InvokeCommand(this, static v => v.ViewModel!.SubmitCertificateCommand));
        bindings.Add(this.BindInteraction(ViewModel, static vm => vm.CertificateBrowseInteraction, BrowseCertificateAsync));
    }

    /// <summary>Stops auto-scroll when any part of the page area is clicked.</summary>
    /// <param name="e">The pointer press.</param>
    private void StopAutoScrollOnPagePress(PointerPressedEventArgs e)
    {
        if (ViewModel is not { IsAutoScrolling: true } tab)
        {
            return;
        }

        tab.IsAutoScrolling = false;
        _ = Canvas.Focus();
        e.Handled = true;
    }

    /// <summary>Asks the desktop for a local PKCS #12 certificate file.</summary>
    /// <param name="context">The certificate path request.</param>
    /// <returns>A task for the file picker.</returns>
    private async Task BrowseCertificateAsync(IInteractionContext<RxVoid, string?> context)
    {
        if (TopLevel.GetTopLevel(this) is not { } owner)
        {
            context.SetOutput(null);
            return;
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Choose a Certificate",
            AllowMultiple = false,
            FileTypeFilter = [new("Certificates (.p12, .pfx)") { Patterns = ["*.p12", "*.pfx"] }],
        });
        context.SetOutput(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }

    /// <summary>Wires the menu items whose commands belong to the window rather than the tab.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindWindowCommands(MultipleDisposable bindings)
    {
        if (TopLevel.GetTopLevel(this) is not MainWindow { ViewModel: { } main })
        {
            return;
        }

        // The commands belong to the window's view model, not this view's, so the binding starts from that source.
        bindings.Add(main.BindOneWay(this, static vm => vm.ShowInFolderCommand, static v => v.ShowInFolderItem.Command, static command => (ICommand?)command));
        bindings.Add(main.BindOneWay(this, static vm => vm.PropertiesCommand, static v => v.PropertiesItem.Command, static command => (ICommand?)command));
    }

    /// <summary>Binds the editor placed over the text field being filled in.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindFieldEditor(MultipleDisposable bindings)
    {
        bindings.Add(this.Bind(ViewModel, static vm => vm.Forms.EditText, static v => v.FieldEditor.Text, static text => text, static text => text ?? string.Empty));
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.Forms.Editing).SubscribeSafe(ShowFieldEditor, OnError));
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.Forms.Focused).SubscribeSafe(_ => Canvas.InvalidateVisual(), OnError));
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.RegionsVersion, static v => v.ViewModel!.Annotations.Tool, static (version, _) => version)
            .SubscribeSafe(_ => Canvas.InvalidateVisual(), OnError));
        bindings.Add(EscapePresses(FieldEditor).SubscribeSafe(_ => ViewModel?.Forms.Cancel(), OnError));
        bindings.Add(FieldEditor.ObserveRouted(InputElement.KeyDownEvent, RoutingStrategies.Tunnel).Where(static args => args.Key == Key.Tab).SubscribeSafe(OnFieldTab, OnError));

        // ShowFieldEditor sets AcceptsReturn from the field, so a single-line field commits on Enter.
        bindings.Add(FieldEditor.Events().KeyDown
            .Where(static args => args.Key == Key.Enter && args.KeyModifiers == KeyModifiers.None && args.Source is TextBox { AcceptsReturn: false })
            .SubscribeSafe(_ => ViewModel?.Forms.Commit(), OnError));
        bindings.Add(FocusLosses(FieldEditor).Where(static _ => !FocusAnnouncementRepair.IsRepeating).SubscribeSafe(_ => ViewModel?.Forms.Commit(), OnError));
        bindings.Add(this.WhenChanged(static v => v.ViewModel!.Forms.EditText, static v => v.ViewModel!.Forms.KeptWordsVersion, static (text, _) => text)
            .SubscribeSafe(UpdateSpelling, OnError));
        bindings.Add(FieldEditor.ObserveRouted(ScrollViewer.ScrollChangedEvent, RoutingStrategies.Bubble, handledEventsToo: true)
            .SubscribeSafe(_ => FieldSpelling.InvalidateVisual(), OnError));
        bindings.Add(FieldEditor.ObserveRouted(ContextRequestedEvent, RoutingStrategies.Tunnel).SubscribeSafe(OnFieldContextRequested, OnError));
    }

    /// <summary>Marks the misspelled words in the field being edited, and says how many there are to screen readers.</summary>
    /// <param name="text">The field's text.</param>
    private void UpdateSpelling(string text)
    {
        if (ViewModel is not { } tab || (tab.Forms.Editing is null && _misspelled.Count == 0))
        {
            // No field is being edited and nothing is marked, so there is nothing to check or clear.
            return;
        }

        tab.Forms.FindMisspelled(text ?? string.Empty, _misspelled);
        FieldSpelling.Show(_misspelled);
        Avalonia.Automation.AutomationProperties.SetHelpText(FieldEditor, DescribeSpelling(text ?? string.Empty, _misspelled));
    }

    /// <summary>Offers corrections for a misspelled word when the editor's menu is asked for, keeping the normal menu otherwise.</summary>
    /// <param name="e">The request.</param>
    private void OnFieldContextRequested(ContextRequestedEventArgs e)
    {
        if (ViewModel is not { } tab || FieldEditor.Text is not { Length: > 0 })
        {
            return;
        }

        var index = FieldEditor.CaretIndex;
        if (SpellingUnderlines.FindPresenter(FieldEditor) is { } presenter && e.TryGetPosition(presenter, out var point))
        {
            index = presenter.TextLayout.HitTestPoint(point).TextPosition;
        }

        var suggestions = tab.Forms.SuggestAt(index, out var word);
        if (word.IsEmpty || !Contains(_misspelled, word))
        {
            return;
        }

        var misspelled = FieldEditor.Text.Substring(word.Start, word.Length);
        List<Control> items = [];
        foreach (var suggestion in suggestions)
        {
            items.Add(new MenuItem { Header = suggestion, Command = tab.Forms.CorrectCommand, CommandParameter = new SpellingFix(word, suggestion) });
        }

        if (items.Count == 0)
        {
            items.Add(new MenuItem { Header = "No suggestions", IsEnabled = false });
        }

        items.Add(new Separator());
        items.Add(new MenuItem { Header = "_Keep This Spelling", Command = tab.Forms.KeepSpellingCommand, CommandParameter = misspelled });
        new ContextMenu { ItemsSource = items }.Open(FieldEditor);
        e.Handled = true;
    }

    /// <summary>Places the editor over a field and focuses it, or hides it.</summary>
    /// <param name="field">The field being edited, or <see langword="null"/>.</param>
    private void ShowFieldEditor(Core.Forms.FormField? field)
    {
        if (field is null)
        {
            FieldEditor.IsVisible = false;
            FieldSpelling.IsVisible = false;
            _ = Canvas.Focus();
            return;
        }

        var rect = Canvas.GetCanvasRect(field.PageIndex, field.Bounds);
        Avalonia.Controls.Canvas.SetLeft(FieldEditor, rect.X);
        Avalonia.Controls.Canvas.SetTop(FieldEditor, rect.Y);
        FieldEditor.Width = rect.Width;
        FieldEditor.Height = rect.Height;
        FieldEditor.AcceptsReturn = field.IsMultiline && !field.IsComb;
        Avalonia.Automation.AutomationProperties.SetName(FieldEditor, string.IsNullOrWhiteSpace(field.Name) ? "Form field" : field.Name);
        FieldEditor.FontSize = field.FontSize > 0 ? field.FontSize * Canvas.PageScale : Math.Max(MinFieldFontSize, rect.Height * FieldFontShare);
        FieldEditor.MaxLength = field.MaxLength;
        SpaceCombLetters(field, rect.Width);
        FieldEditor.IsVisible = true;
        Avalonia.Controls.Canvas.SetLeft(FieldSpelling, rect.X);
        Avalonia.Controls.Canvas.SetTop(FieldSpelling, rect.Y);
        FieldSpelling.Width = rect.Width;
        FieldSpelling.Height = rect.Height;
        FieldSpelling.IsVisible = true;
        _ = OnMainThread().SubscribeSafe(_ => FocusFieldEditor(), OnError);
    }

    /// <summary>Spreads a comb field's letters one to a box, in a fixed width font; other fields type normally.</summary>
    /// <param name="field">The field.</param>
    /// <param name="width">The field's width on the canvas.</param>
    private void SpaceCombLetters(Core.Forms.FormField field, double width)
    {
        if (!field.IsComb)
        {
            FieldEditor.ClearValue(TemplatedControl.LetterSpacingProperty);
            FieldEditor.ClearValue(TemplatedControl.FontFamilyProperty);
            FieldEditor.ClearValue(TextBox.PaddingProperty);
            return;
        }

        var font = PageFonts.Get(Core.Text.StandardFontFamilies.Mono);
        var cell = width / field.MaxLength;
        var advance = new FormattedText("0", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(font), FieldEditor.FontSize, null).WidthIncludingTrailingWhitespace;
        FieldEditor.FontFamily = font;
        FieldEditor.LetterSpacing = cell - advance;
        FieldEditor.Padding = new((cell - advance) * HalfCell, 0, 0, 0);
    }

    /// <summary>Moves to the next field on Tab, or the previous one on Shift+Tab.</summary>
    /// <param name="e">The key press.</param>
    private void OnFieldTab(KeyEventArgs e)
    {
        e.Handled = true;
        if (ViewModel?.Forms.CommitAndMove((e.KeyModifiers & KeyModifiers.Shift) != 0) is not { } next || next.Kind == Core.Forms.FormFieldKind.Text)
        {
            return;
        }

        // The editor closes for a box or list, so the pages take the keys that fill it in.
        ViewModel.NavigateTo(new(next.PageIndex, next.Bounds, 0));
        Canvas.InvalidateVisual();
    }

    /// <summary>Shows the window that makes a signature or initials.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowSignatureMarkAsync(IInteractionContext<SignatureMarkViewModel, bool> context)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            context.SetOutput(false);
            return;
        }

        context.SetOutput(await new SignatureMarkWindow { ViewModel = context.Input }.ShowDialog<bool>(owner));
    }

    /// <summary>Shows the checked signatures.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowSignaturesAsync(IInteractionContext<SignaturesViewModel, RxVoid> context)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await new SignaturesWindow { ViewModel = context.Input }.ShowDialog(owner);
        }

        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Asks the user for text.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task PromptAsync(IInteractionContext<TextPrompt, string?> context)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            context.SetOutput(null);
            return;
        }

        context.SetOutput(await new PromptWindow { ViewModel = new(context.Input) }.ShowDialog<string?>(owner));
    }

    /// <summary>Asks where to save a copy, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveAsAsync(IInteractionContext<string, string?> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput(null);
            return;
        }

        var file = await storage.SaveFilePickerAsync(new() { Title = "Save a Copy", SuggestedFileName = context.Input, DefaultExtension = "pdf" });
        context.SetOutput(file?.TryGetLocalPath());
    }

    /// <summary>Asks where to save an image of part of a page, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveImageAsync(IInteractionContext<string, string?> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput(null);
            return;
        }

        var file = await storage.SaveFilePickerAsync(new() { Title = "Save Image of Area", SuggestedFileName = context.Input, DefaultExtension = "png" });
        context.SetOutput(file?.TryGetLocalPath());
    }

    /// <summary>Shows the "Sign with Certificate" window.</summary>
    /// <param name="context">The interaction context, holding the request.</param>
    /// <returns>A task.</returns>
    private async Task ShowCertificateSignAsync(IInteractionContext<CertificateSignViewModel, bool> context)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            context.SetOutput(false);
            return;
        }

        context.SetOutput(await new CertificateSignWindow { ViewModel = context.Input }.ShowDialog<bool>(owner));
    }

    /// <summary>Shows the "Save Optimised Copy" window.</summary>
    /// <param name="context">The interaction context, holding the request.</param>
    /// <returns>A task.</returns>
    private async Task ShowOptimizeAsync(IInteractionContext<OptimizeCopyViewModel, RxVoid> context)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await new OptimizeCopyWindow { ViewModel = context.Input }.ShowDialog(owner);
        }

        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Shows the "Save PDF/A Copy" window.</summary>
    /// <param name="context">The interaction context, holding the request.</param>
    /// <returns>A task.</returns>
    private async Task ShowArchiveAsync(IInteractionContext<ArchiveCopyViewModel, RxVoid> context)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await new ArchiveCopyWindow { ViewModel = context.Input }.ShowDialog(owner);
        }

        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Binds the menu items that write a new copy: the optimised, PDF/A and redacted copies, with their windows.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindRedaction(MultipleDisposable bindings)
    {
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.OptimizeCopyCommand, static v => v.OptimizeCopyItem));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.OptimizeMenuText, static v => v.OptimizeCopyItem.Header));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.OptimizeInteraction), ShowOptimizeAsync));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ArchiveCopyCommand, static v => v.ArchiveCopyItem));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ArchiveMenuText, static v => v.ArchiveCopyItem.Header));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.ArchiveInteraction), ShowArchiveAsync));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ApplyRedactionsCommand, static v => v.ApplyRedactionsItem));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.RedactMenuText, static v => v.ApplyRedactionsItem.Header));
        bindings.Add(HandleInteraction(this.WhenChanged(static v => v.ViewModel!.RedactInteraction), ShowRedactionAsync));
    }

    /// <summary>Shows the "Apply Redactions" window.</summary>
    /// <param name="context">The interaction context, holding the request.</param>
    /// <returns>A task.</returns>
    private async Task ShowRedactionAsync(IInteractionContext<RedactionViewModel, RxVoid> context)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await new RedactionWindow { ViewModel = context.Input }.ShowDialog(owner);
        }

        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Shows the print preview window.</summary>
    /// <param name="context">The interaction context, holding the preview.</param>
    /// <returns>A task.</returns>
    private async Task ShowPrintPreviewAsync(IInteractionContext<PrintPreviewViewModel, bool> context)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            context.SetOutput(false);
            return;
        }

        context.SetOutput(await new PrintPreviewWindow { ViewModel = context.Input }.ShowDialog<bool>(owner));
    }

    /// <summary>Asks where to save an embedded file, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveAttachmentAsync(IInteractionContext<string, string?> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput(null);
            return;
        }

        var file = await storage.SaveFilePickerAsync(new() { Title = "Save Attachment", SuggestedFileName = context.Input });
        context.SetOutput(file?.TryGetLocalPath());
    }

    /// <summary>Opens the menu of the annotation under the pointer in the sidebar: edit its note or delete it.</summary>
    /// <param name="e">The event.</param>
    private void OnAnnotationContextRequested(ContextRequestedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is not AnnotationItemViewModel item || ViewModel is not { } tab || e.Source is not Control source)
        {
            return;
        }

        var menu = new ContextMenu
        {
            ItemsSource = new Control[]
            {
                new MenuItem { Header = "_Edit Note…", Command = tab.Annotations.EditNoteCommand, CommandParameter = item },
                new MenuItem { Header = "_Delete", Command = tab.Annotations.DeleteCommand, CommandParameter = item },
            },
        };
        menu.Open(source);
        e.Handled = true;
    }

    /// <summary>Asks before a linked or attached file opens in another app.</summary>
    /// <param name="context">The file's name in; <see langword="true"/> out to open it.</param>
    /// <returns>A task.</returns>
    private async Task ConfirmOpenFileAsync(IInteractionContext<string, bool> context)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            context.SetOutput(false);
            return;
        }

        ConfirmRequest request = new("Open File", $"Open {context.Input} in another app? Only open files you trust.", "Open File");
        context.SetOutput(await new ConfirmWindow { ViewModel = new(request) }.ShowDialog<bool>(owner));
    }

    /// <summary>Opens a file the reader agreed to open with the desktop's default app.</summary>
    /// <param name="path">The file.</param>
    private void LaunchFile(string path)
    {
        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchFileInfoAsync(new(path));
        }
    }

    /// <summary>Opens external links with the desktop's default handler.</summary>
    /// <param name="uri">The URI.</param>
    private void OpenUri(Uri uri)
    {
        if (uri.Scheme is "http" or "https" or "mailto" && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }

    /// <summary>Drops thumbnail requests for items scrolled out of view and re-requests the visible ones.</summary>
    private void OnThumbnailsScrolled()
    {
        if (ViewModel is not { } tab)
        {
            return;
        }

        _ = tab.ThumbnailClient.Advance();
        foreach (var visual in ThumbnailList.GetVisualDescendants())
        {
            if (visual is PageThumbnail thumbnail)
            {
                thumbnail.InvalidateVisual();
            }
        }
    }
}
