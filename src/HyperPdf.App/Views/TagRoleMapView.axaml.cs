// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The custom tag types of the document and what each stands for, with the controls that add and remove them.</summary>
[DebuggerDisplay("TagRoleMapView: {ViewModel}")]
public sealed partial class TagRoleMapView : ReactiveUI.Avalonia.ReactiveUserControl<TagsViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TagRoleMapView"/> class.</summary>
    public TagRoleMapView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RoleMappings, static v => v.RoleList.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.RoleMappingIndex, static v => v.RoleList.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.NewRoleType, static v => v.NewRoleBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.StandardTypes, static v => v.RoleTargetBox.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.NewRoleTarget, static v => v.RoleTargetBox.SelectedItem, static target => target, static item => item as string));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.AddRoleMappingCommand, static v => v.AddRoleButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.RemoveRoleMappingCommand, static v => v.RemoveRoleButton));
        });
    }
}
