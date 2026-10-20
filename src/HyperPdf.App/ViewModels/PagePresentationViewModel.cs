// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.App.Services;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Presentation;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// What happens as each page appears while a tab presents: the effect the page asks for and the time before it moves on. Both
/// are followed only when the reader allows them and motion is not reduced. The reader can always move pages themselves, which
/// restarts any wait, and nothing changes by itself unless the settings say so.
/// </summary>
[DebuggerDisplay("PagePresentationViewModel: {Current}")]
public sealed partial class PagePresentationViewModel : ReactiveObject, IDisposable
{
    /// <summary>The tab that presents.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The application services holding the settings and the theme.</summary>
    private readonly AppServices _services;

    /// <summary>Subscriptions kept for the life of the tab.</summary>
    private readonly MultipleDisposable _subscriptions;

    /// <summary>The number given to the last step, so every page shown is a new step.</summary>
    private int _sequence;

    /// <summary>Whether the reader has been told about an approximated style in this presentation.</summary>
    private bool _approximationTold;

    /// <summary>Initializes a new instance of the <see cref="PagePresentationViewModel"/> class.</summary>
    /// <param name="owner">The tab that presents.</param>
    /// <param name="services">The application services.</param>
    public PagePresentationViewModel(DocumentTabViewModel owner, AppServices services)
    {
        _owner = owner;
        _services = services;
        _subscriptions =
        [
            owner.WhenChanged(static tab => tab.IsPresenting, static tab => tab.CurrentPageIndex, static (presenting, page) => new Showing(presenting, page))
                .DistinctUntilChanged()
                .SubscribeSafe(Show, ErrorTrace.Write),

            // Each step waits out the time its page asked for; the next step, or the end of presenting, ends the wait.
            this.WhenChanged(static vm => vm.Current)
                .SwitchMap(WaitForAdvance)
                .SubscribeSafe(_ => Advance(), ErrorTrace.Write),
        ];
    }

    /// <summary>Gets the step for the page now shown, or <see langword="null"/> when the tab is not presenting.</summary>
    [Reactive]
    public partial PresentationStep? Current { get; private set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Waits the time a step's page asked to stay, or never when it asked for none.</summary>
    /// <param name="step">The step.</param>
    /// <returns>A stream that ticks once when the wait is over.</returns>
    private static IObservable<RxVoid> WaitForAdvance(PresentationStep? step) =>
        step?.AdvanceAfterSeconds is { } seconds
            ? Signal.Timer(TimeSpan.FromSeconds(seconds), RxSchedulers.MainThreadScheduler).Select(static _ => RxVoid.Default)
            : Signal.Never<RxVoid>();

    /// <summary>Plans how the page now shown appears.</summary>
    /// <param name="showing">Whether the tab presents, and the page shown.</param>
    private void Show(Showing showing)
    {
        if (!showing.Presenting)
        {
            _approximationTold = false;
            Current = null;
            return;
        }

        var source = DocumentFeature<IPageTransitionSource>.Get(_owner.TryGetDocument());
        var settings = _services.Settings;
        PresentationOptions options = new(settings.FollowPageTransitions, settings.FollowPageTimings, _services.CurrentTheme.ReduceMotion);
        _sequence++;
        var step = PresentationPlanner.Plan(_sequence, source?.GetTransition(showing.Page), options);
        Current = step;
        if (!step.IsApproximate || _approximationTold)
        {
            return;
        }

        _approximationTold = true;
        _owner.Notice = $"A page asks for a {step.Style} transition. It is shown as a simpler effect.";
    }

    /// <summary>Moves to the next page when the page shown asked to move on, unless it is the last.</summary>
    private void Advance()
    {
        if (_owner.IsPresenting && _owner.CurrentPageIndex + 1 < _owner.PageCount)
        {
            _owner.GoToPage(_owner.CurrentPageIndex + 1);
        }
    }

    /// <summary>Whether the tab presents, and the page it shows.</summary>
    /// <param name="Presenting">Whether the tab presents.</param>
    /// <param name="Page">The zero based page shown.</param>
    private readonly record struct Showing(bool Presenting, int Page);
}
