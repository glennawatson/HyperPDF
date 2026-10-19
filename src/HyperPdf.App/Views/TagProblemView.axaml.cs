// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>One thing the accessibility check found.</summary>
[DebuggerDisplay("TagProblemView: {ViewModel}")]
public sealed partial class TagProblemView : ReactiveUI.Avalonia.ReactiveUserControl<TagProblemItem>
{
    /// <summary>Initializes a new instance of the <see cref="TagProblemView"/> class.</summary>
    public TagProblemView()
    {
        InitializeComponent();

        // A problem is an immutable record with no change notification, so OneWayBind cannot observe it.
        // The view follows which one it shows instead.
        _ = this.WhenActivated(disposables => disposables.Add(this.WhenChanged(static v => v.ViewModel).SubscribeSafe(Show, ErrorTrace.Write)));
    }

    /// <summary>Shows a problem.</summary>
    /// <param name="problem">The problem.</param>
    private void Show(TagProblemItem? problem) => ProblemText.Text = problem?.Text;
}
