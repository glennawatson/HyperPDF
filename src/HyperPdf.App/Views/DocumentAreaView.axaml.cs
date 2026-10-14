// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The selected document, and a second view of it beside the first while the view is split.</summary>
[DebuggerDisplay("DocumentAreaView: {ViewModel}")]
public sealed partial class DocumentAreaView : ReactiveUI.Avalonia.ReactiveUserControl<MainViewModel>
{
    /// <summary>The column of the second view while split.</summary>
    private const int SecondViewColumn = 2;

    /// <summary>Initializes a new instance of the <see cref="DocumentAreaView"/> class.</summary>
    public DocumentAreaView()
    {
        InitializeComponent();
        DocumentHost.ContentTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new DocumentView(), true);
        SplitHost.ContentTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new DocumentView(), true);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasTabs, static v => v.DocumentHost.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SelectedTab, static v => v.DocumentHost.Content));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SplitTab, static v => v.SplitHost.Content));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SplitTab, static v => v.SplitHost.IsVisible, static split => split is not null));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SplitTab, static v => v.SplitSplitter.IsVisible, static split => split is not null));

            // The width is shared between the two views while split, and given back to the first afterwards.
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.SplitTab)
                .Select(static split => split is null ? new GridLength(0) : new GridLength(1, GridUnitType.Star))
                .SubscribeSafe(width => DocumentArea.ColumnDefinitions[SecondViewColumn].Width = width, ErrorTrace.Write));
        });
    }

    /// <summary>Finds the visible document view.</summary>
    /// <returns>The view, if any.</returns>
    internal DocumentView? FindDocumentView()
    {
        foreach (var visual in DocumentHost.GetVisualDescendants())
        {
            if (visual is DocumentView view)
            {
                return view;
            }
        }

        return null;
    }
}
