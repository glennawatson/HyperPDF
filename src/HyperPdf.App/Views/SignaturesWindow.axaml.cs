// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Signatures;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>Lists a document's checked digital signatures in plain words.</summary>
[DebuggerDisplay("SignaturesWindow: {Title}")]
public sealed partial class SignaturesWindow : ReactiveUI.Avalonia.ReactiveWindow<SignaturesViewModel>
{
    /// <summary>The space between the lines of one signature.</summary>
    private const double LineSpacing = 4;

    /// <summary>The space below each signature.</summary>
    private const double EntryGap = 12;

    /// <summary>The heading level of each signer's name; the window title is the level above.</summary>
    private const int SignerHeadingLevel = 2;

    /// <summary>Initializes a new instance of the <see cref="SignaturesWindow"/> class.</summary>
    public SignaturesWindow()
    {
        InitializeComponent();
        SignatureList.ItemTemplate = new FuncDataTemplate<DocumentSignature>(static (signature, _) => CreateEntry(signature));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Signatures, static v => v.SignatureList.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.Title, static name => $"Signatures — {name}"));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CloseCommand, static v => v.CloseButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.CloseCommand)
                .SwitchMap(static closed => closed)
                .SubscribeSafe(_ => Close(), static error => Trace.TraceError(error.ToString())));
            _ = CloseButton.Focus();
        });
    }

    /// <summary>Creates the entry of one signature: who, when, why, and what the check found.</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>The entry.</returns>
    private static StackPanel CreateEntry(DocumentSignature? signature)
    {
        var panel = new StackPanel { Spacing = LineSpacing, Margin = new(0, 0, 0, EntryGap) };
        if (signature is null)
        {
            return panel;
        }

        var who = signature.SignerName.Length > 0 ? signature.SignerName : "Unknown signer";
        who = signature.IsDocumentTimestamp ? $"Document timestamp by {who}" : who;
        var heading = new TextBlock { Text = who, FontWeight = FontWeight.SemiBold };

        // Each signer starts a section, so a screen reader can move from one signature to the next by heading.
        AutomationProperties.SetHeadingLevel(heading, SignerHeadingLevel);
        panel.Children.Add(heading);
        panel.Children.Add(new TextBlock { Text = signature.Summary, TextWrapping = TextWrapping.Wrap });
        AddDetail(panel, signature.SigningTime is { } time ? string.Create(CultureInfo.CurrentCulture, $"Signed {time.ToLocalTime():f}") : null);
        AddDetail(panel, signature.Reason.Length > 0 ? $"Reason: {signature.Reason}" : null);
        AddDetail(panel, signature.Issuer.Length > 0 ? $"Certificate issued by {signature.Issuer}" : null);
        AddDetail(panel, signature.TimestampSummary.Length > 0 ? signature.TimestampSummary : null);
        AddDetail(panel, signature.Detail.Length > 0 ? signature.Detail : null);
        return panel;
    }

    /// <summary>Adds a secondary line when there is something to say.</summary>
    /// <param name="panel">The entry.</param>
    /// <param name="text">The line, or <see langword="null"/>.</param>
    private static void AddDetail(StackPanel panel, string? text)
    {
        if (text is null)
        {
            return;
        }

        var line = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        line.Classes.Add("secondary");
        panel.Children.Add(line);
    }
}
