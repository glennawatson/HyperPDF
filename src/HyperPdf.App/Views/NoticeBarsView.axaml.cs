// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The notices a document shows above its pages: what it holds that cannot be shown, repairs, requirements, messages and a changed file.</summary>
[DebuggerDisplay("NoticeBarsView: {ViewModel}")]
public sealed partial class NoticeBarsView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>The collapsed detail action's resource key.</summary>
    private const string DetailsKey = "Details";

    /// <summary>The expanded detail action's resource key.</summary>
    private const string HideDetailsKey = "Hide details";

    /// <summary>Initializes a new instance of the <see cref="NoticeBarsView"/> class.</summary>
    public NoticeBarsView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(UiText.CultureChanges.SubscribeSafe(_ => RefreshCultureText(), ErrorTrace.Write));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notices.ContentWarning, static v => v.ContentWarningText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notices.ContentWarning, static v => v.ContentWarningBar.IsVisible, static warning => warning is not null));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Notices.DismissContentWarningCommand, static v => v.DismissContentWarningButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notices.IntegrityNotice, static v => v.IntegrityNoticeText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notices.IntegrityNotice, static v => v.IntegrityNoticeBar.IsVisible, static notice => notice is not null));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Notices.DismissIntegrityNoticeCommand, static v => v.DismissIntegrityNoticeButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notices.RepairNotice, static v => v.RepairNoticeText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notices.RepairNotice, static v => v.RepairNoticeBar.IsVisible, static notice => notice is not null));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notices.RepairDetails, static v => v.RepairDetailsText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notices.RepairDetails, static v => v.RepairDetailsText.IsVisible, static details => details is not null));
            disposables.Add(this.OneWayBind(
                ViewModel,
                static vm => vm.Notices.RepairDetails,
                static v => v.ToggleRepairDetailsButton.Content,
                static details => details is null ? UiText.Get(DetailsKey) : UiText.Get(HideDetailsKey)));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Notices.ToggleRepairDetailsCommand, static v => v.ToggleRepairDetailsButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Notices.DismissRepairNoticeCommand, static v => v.DismissRepairNoticeButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notices.RequirementNotice, static v => v.RequirementNoticeText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notices.RequirementNotice, static v => v.RequirementNoticeBar.IsVisible, static notice => notice is not null));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notices.RequirementDetails, static v => v.RequirementDetailsText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notices.RequirementDetails, static v => v.RequirementDetailsText.IsVisible, static details => details is not null));
            disposables.Add(this.OneWayBind(
                ViewModel,
                static vm => vm.Notices.RequirementDetails,
                static v => v.ToggleRequirementDetailsButton.Content,
                static details => details is null ? UiText.Get(DetailsKey) : UiText.Get(HideDetailsKey)));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Notices.ToggleRequirementDetailsCommand, static v => v.ToggleRequirementDetailsButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Notices.DismissRequirementNoticeCommand, static v => v.DismissRequirementNoticeButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notice, static v => v.NoticeText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Notice, static v => v.NoticeBar.IsVisible, static notice => notice is not null));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.DismissNoticeCommand, static v => v.DismissNoticeButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasPendingReload, static v => v.ReloadBar.IsVisible));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ReloadCommand, static v => v.ReloadButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.DismissReloadCommand, static v => v.DismissReloadButton));
        });
    }

    /// <summary>Refreshes translated presentation text without notifying or changing document state.</summary>
    private void RefreshCultureText()
    {
        if (ViewModel is not { } model)
        {
            return;
        }

        ToggleRepairDetailsButton.Content = model.Notices.RepairDetails is null ? UiText.Get(DetailsKey) : UiText.Get(HideDetailsKey);
        ToggleRequirementDetailsButton.Content = model.Notices.RequirementDetails is null ? UiText.Get(DetailsKey) : UiText.Get(HideDetailsKey);
    }
}
