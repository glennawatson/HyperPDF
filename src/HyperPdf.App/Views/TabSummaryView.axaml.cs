// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>One open tab in the tab finder.</summary>
[DebuggerDisplay("TabSummaryView: {ViewModel}")]
public sealed partial class TabSummaryView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TabSummaryView"/> class.</summary>
    public TabSummaryView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.NameText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Folder, static v => v.FolderText.Text));
        });
    }
}
