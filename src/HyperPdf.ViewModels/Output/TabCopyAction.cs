// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Output;

/// <summary>
/// A tab command that opens a window to save a changed copy, such as an optimised, PDF/A or redacted copy. It is off,
/// with a menu text that says why, when the open document's engine cannot make that copy.
/// </summary>
/// <typeparam name="TRequest">The view model of the window the command opens.</typeparam>
[DebuggerDisplay("TabCopyAction: {MenuText}")]
public sealed partial class TabCopyAction<TRequest> : ReactiveObject, IDisposable, ILocalisationChildren
    where TRequest : class, ICopyWindowRequest
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The command's texts, availability and window.</summary>
    private readonly TabCopyActionSpec<TRequest> _spec;

    /// <summary>
    /// Whether the command can run, read from <see cref="CanRun"/>. The binding generator cannot observe a generic type's
    /// properties, so the change notifications ReactiveObject itself publishes are read instead.
    /// </summary>
    private readonly IObservable<bool> _canRun;

    /// <summary>Owns the command.</summary>
    private readonly MultipleDisposable _lifetime = [];

    /// <summary>The window's view model while the window is open, so the tab can close it when its document goes away.</summary>
    private TRequest? _open;

    /// <summary>Initializes a new instance of the <see cref="TabCopyAction{TRequest}"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="spec">The command's texts, availability and window.</param>
    internal TabCopyAction(DocumentTabViewModel owner, TabCopyActionSpec<TRequest> spec)
    {
        _owner = owner;
        _spec = spec;
        MenuText = UiText.Get(spec.UnavailableText);
        _canRun = Changed
            .Where(static change => change.PropertyName == nameof(CanRun))
            .Select(static change => change.Sender is TabCopyAction<TRequest> { CanRun: true })
            .StartWith(false);
        _lifetime.Add(RunCommand);
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
    public void Dispose() => _lifetime.Dispose();

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        MenuText = UiText.Get(CanRun ? _spec.AvailableText : _spec.UnavailableText);
        LocalisationRefresh.RefreshCore(Volatile.Read(ref _open), visited);
        LocalisationRefresh.RefreshCore(_owner, visited);
    }

    /// <summary>Stops the run in the command's window and closes it, because the document it works on is going away.</summary>
    /// <returns><see langword="true"/> when a window was open.</returns>
    internal bool CloseWindow()
    {
        if (Volatile.Read(ref _open) is not { } request)
        {
            return false;
        }

        // Executing a command is a cold stream that completes once the command has run, so the subscription ends by itself.
        _ = request.CloseCommand.Execute().SubscribeSafe(
            static _ =>
{
    },
            ErrorTrace.Write);
        return true;
    }

    /// <summary>Works out whether the open document's engine can make this copy.</summary>
    /// <param name="document">The open document.</param>
    internal void Refresh(IDocument document)
    {
        var can = _spec.IsAvailable(document);
        CanRun = can;
        MenuText = UiText.Get(can ? _spec.AvailableText : _spec.UnavailableText);
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
            Volatile.Write(ref _open, request);
            try
            {
                _ = await Interaction.Handle(request).ToTask().ConfigureAwait(true);
            }
            finally
            {
                Volatile.Write(ref _open, null);
            }
        }
    }
}
