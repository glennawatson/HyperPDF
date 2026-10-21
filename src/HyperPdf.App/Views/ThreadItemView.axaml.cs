// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>One article thread in the parts panel: its title and how many places it is read in.</summary>
[DebuggerDisplay("ThreadItemView: {ViewModel}")]
public sealed partial class ThreadItemView : ReactiveUI.Avalonia.ReactiveUserControl<ThreadItemViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="ThreadItemView"/> class.</summary>
    public ThreadItemView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Title, static v => v.TitleText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Summary, static v => v.SummaryText.Text));
        });
    }
}
