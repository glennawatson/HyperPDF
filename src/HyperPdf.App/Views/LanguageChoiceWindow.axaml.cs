// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.Services;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The first-launch language dialog: the app changes language as each choice is picked, and Continue keeps it.</summary>
[DebuggerDisplay("LanguageChoiceWindow: {ViewModel}")]
public sealed partial class LanguageChoiceWindow : ReactiveUI.Avalonia.ReactiveWindow<LanguageChoiceViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="LanguageChoiceWindow"/> class.</summary>
    public LanguageChoiceWindow()
    {
        InitializeComponent();
        LanguageList.ItemsSource = LanguageChoiceViewModel.Options;
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.Bind(ViewModel, static vm => vm.SelectedIndex, static v => v.LanguageList.SelectedIndex));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.SelectedIndex)
                .DistinctUntilChanged()
                .Skip(1)
                .SubscribeSafe(LanguagePreferences.Change, ErrorTrace.Write));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ContinueCommand, static v => v.ContinueButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.ContinueCommand)
                .SwitchMap(static command => command)
                .SubscribeSafe(_ => Close(), ErrorTrace.Write));

            _ = ContinueButton.Focus();
        });
    }
}
