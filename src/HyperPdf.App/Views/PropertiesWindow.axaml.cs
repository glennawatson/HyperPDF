// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>Shows a document's properties. The properties do not change while it is open, so it fills itself once.</summary>
[DebuggerDisplay("PropertiesWindow: {Title}")]
public sealed partial class PropertiesWindow : ReactiveUI.Avalonia.ReactiveWindow<PropertiesViewModel>
{
    /// <summary>The width of the name column.</summary>
    private const double NameColumnWidth = 120;

    /// <summary>The space between the name and the value.</summary>
    private const double ColumnGap = 12;

    /// <summary>The vertical space around each row.</summary>
    private const double RowGap = 3;

    /// <summary>Initializes a new instance of the <see cref="PropertiesWindow"/> class.</summary>
    public PropertiesWindow()
    {
        InitializeComponent();
        EntryList.ItemTemplate = new FuncDataTemplate<PropertyEntry>(static (entry, _) => CreateRow(entry));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Title, static v => v.Title, static title => $"Properties — {title}"));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Entries, static v => v.EntryList.ItemsSource));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), ErrorTrace.Write));
            _ = CloseButton.Focus();
        });
    }

    /// <summary>Creates one name and value row.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The row.</returns>
    private static Grid CreateRow(PropertyEntry? entry)
    {
        var name = new TextBlock { Text = entry?.Name, HorizontalAlignment = HorizontalAlignment.Right, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new(0, 0, ColumnGap, 0) };
        name.Classes.Add("secondary");
        var value = new SelectableTextBlock { Text = entry?.Value, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        Grid.SetColumn(value, 1);
        FieldLabels.Link((value, name));
        return new Grid { ColumnDefinitions = [new(NameColumnWidth, GridUnitType.Pixel), new(1, GridUnitType.Star)], Margin = new(0, RowGap), Children = { name, value } };
    }
}
