// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>One tag in the tags list, indented to show how deep it is.</summary>
[DebuggerDisplay("TagItemView: {ViewModel}")]
public sealed partial class TagItemView : ReactiveUI.Avalonia.ReactiveUserControl<TagItemViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TagItemView"/> class.</summary>
    public TagItemView()
    {
        InitializeComponent();

        // A tag row is an immutable record with no change notification, so OneWayBind cannot observe it.
        // The view follows which one it shows instead.
        _ = this.WhenActivated(disposables => disposables.Add(this.WhenChanged(static v => v.ViewModel).SubscribeSafe(Show, ErrorTrace.Write)));
    }

    /// <summary>Shows a tag.</summary>
    /// <param name="tag">The tag.</param>
    private void Show(TagItemViewModel? tag)
    {
        RowText.Text = tag?.Row;
        RowText.Margin = new(tag?.IndentWidth ?? 0, 0, 0, 0);
    }
}
