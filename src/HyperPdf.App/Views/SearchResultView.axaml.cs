// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>One search result in the sidebar.</summary>
[DebuggerDisplay("SearchResultView: {ViewModel}")]
public sealed partial class SearchResultView : ReactiveUI.Avalonia.ReactiveUserControl<SearchResultItemViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="SearchResultView"/> class.</summary>
    public SearchResultView()
    {
        InitializeComponent();

        // A search result is an immutable record with no change notification, so OneWayBind cannot observe it.
        // The view follows which one it shows instead.
        _ = this.WhenActivated(disposables => disposables.Add(this.WhenChanged(static v => v.ViewModel).SubscribeSafe(Show, ErrorTrace.Write)));
    }

    /// <summary>Shows a search result.</summary>
    /// <param name="result">The result.</param>
    private void Show(SearchResultItemViewModel? result)
    {
        PageText.Text = result is null ? null : string.Create(CultureInfo.CurrentCulture, $"Page {result.PageNumber}");
        ContextText.Text = result?.Context;
    }
}
