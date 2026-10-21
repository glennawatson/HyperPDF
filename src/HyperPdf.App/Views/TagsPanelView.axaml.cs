// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using HyperPdf.App.Controls;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>The tags of a document as a list, with the check for problems. The form, the order buttons and the custom tag types are sub controls.</summary>
[DebuggerDisplay("TagsPanelView: {ViewModel}")]
public sealed partial class TagsPanelView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>The keys held with an arrow to move a tag in the reading order.</summary>
    private const KeyModifiers ReorderKeys = KeyModifiers.Control | KeyModifiers.Shift;

    /// <summary>Initializes a new instance of the <see cref="TagsPanelView"/> class.</summary>
    public TagsPanelView()
    {
        InitializeComponent();
        TagList.ItemTemplate = new FuncDataTemplate<TagItemViewModel>(static (_, _) => new TagItemView());
        ProblemList.ItemTemplate = new FuncDataTemplate<TagProblemItem>(static (_, _) => new TagProblemView());
        var keys = TagList.TunnelKeyDowns;
        _ = this.WhenActivated(disposables =>
        {
            // Items are named as their containers are prepared, so this comes before the lists are filled.
            disposables.Add(ItemAutomation.NameItems(TagList));
            disposables.Add(ItemAutomation.NameItems(ProblemList));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsTagsMode, static v => v.TagsPanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tags, static v => v.TagForm.ViewModel));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tags, static v => v.TagOrder.ViewModel));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tags, static v => v.TagRoleMap.ViewModel));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tags.Items, static v => v.TagList.ItemsSource));
            disposables.Add(this.Bind(
                ViewModel,
                static vm => vm.Tags.SelectedItem,
                static v => v.TagList.SelectedItem,
                static item => item,
                static item => item as TagItemViewModel));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tags.IsTagged, static v => v.TagList.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tags.IsTagged, static v => v.UntaggedText.IsVisible, static tagged => !tagged));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tags.IsTagged, static v => v.FormScroll.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tags.Problems, static v => v.ProblemList.ItemsSource));
            disposables.Add(this.Bind(
                ViewModel,
                static vm => vm.Tags.SelectedProblem,
                static v => v.ProblemList.SelectedItem,
                static item => item,
                static item => item as TagProblemItem));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tags.ProblemSummary, static v => v.SummaryText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Tags.Status, static v => v.StatusText.Text));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Tags.CheckCommand, static v => v.CheckButton));
            disposables.Add(keys.Chord(new(Key.Right, ReorderKeys)).MarkHandled().AsVoid.InvokeCommand(this, static v => v.ViewModel!.Tags.NestCommand));
            disposables.Add(keys.Chord(new(Key.Left, ReorderKeys)).MarkHandled().AsVoid.InvokeCommand(this, static v => v.ViewModel!.Tags.UnnestCommand));
            disposables.Add(keys.Chord(new(Key.Delete, KeyModifiers.None)).MarkHandled().AsVoid.InvokeCommand(this, static v => v.ViewModel!.Tags.RemoveCommand));
            disposables.Add(keys.Chord(new(Key.Up, ReorderKeys)).MarkHandled().AsVoid.InvokeCommand(this, static v => v.ViewModel!.Tags.MoveUpCommand));
            disposables.Add(keys.Chord(new(Key.Down, ReorderKeys)).MarkHandled().AsVoid.InvokeCommand(this, static v => v.ViewModel!.Tags.MoveDownCommand));

            // A change replaces the rows, so a row that held the keyboard focus is gone; the focus goes back to the chosen tag.
            disposables.Add(this.WhenChanged(static v => v.ViewModel)
                .SwitchMap(static tab => tab is null ? Signal.Never<int>() : tab.Tags.WhenChanged(static tags => tags.EditVersion).Skip(1))
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .WhileFocusOutside(FormScroll)
                .InjectLatest(this.WhenChanged(static v => v.ViewModel))
                .Select(static tab => tab.Tags.SelectedItem)
                .RowsIn(TagList)
                .SubscribeSafe(static row => _ = row.Focus(), ErrorTrace.Write));
        });
    }
}
