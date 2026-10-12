// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// A tab command that opens a window to save a changed copy, such as an optimised, PDF/A or redacted copy. It is off,
/// with a menu text that says why, when the open document's engine cannot make that copy.
/// </summary>
/// <typeparam name="TRequest">The view model of the window the command opens.</typeparam>
[DebuggerDisplay("TabCopyAction: {MenuText}")]
public sealed partial class TabCopyAction<TRequest> : ReactiveObject, IDisposable
    where TRequest : class, IDisposable
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The command's texts, availability and window.</summary>
    private readonly TabCopyActionSpec<TRequest> _spec;

    /// <summary>Whether the command can run; a signal, as generated bindings cannot observe a generic type's properties.</summary>
    private readonly BehaviorSignal<bool> _canRun = new(false);

    /// <summary>Initializes a new instance of the <see cref="TabCopyAction{TRequest}"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="spec">The command's texts, availability and window.</param>
    internal TabCopyAction(DocumentTabViewModel owner, TabCopyActionSpec<TRequest> spec)
    {
        _owner = owner;
        _spec = spec;
        MenuText = spec.UnavailableText;
    }

    /// <summary>Gets a value indicating whether the open document's engine can make this copy.</summary>
    [Reactive]
    public partial bool CanRun { get; private set; }

    /// <summary>Gets the menu text; when the command is off, the text says why.</summary>
    [Reactive]
    public partial string MenuText { get; private set; } = string.Empty;

    /// <summary>Gets the interaction showing the command's window.</summary>
    public Interaction<TRequest, RxVoid> Interaction { get; } = new();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _canRun.Dispose();

    /// <summary>Works out whether the open document's engine can make this copy.</summary>
    /// <param name="document">The open document.</param>
    internal void Refresh(IDocument document)
    {
        var can = _spec.IsAvailable(document);
        CanRun = can;
        MenuText = can ? _spec.AvailableText : _spec.UnavailableText;
        _canRun.OnNext(can);
    }

    /// <summary>Shows the command's window for the tab's document.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canRun))]
    private async Task RunAsync()
    {
        if (_spec.CreateRequest(_owner) is not { } request)
        {
            return;
        }

        using (request)
        {
            _ = await Interaction.Handle(request).ToTask().ConfigureAwait(true);
        }
    }
}
