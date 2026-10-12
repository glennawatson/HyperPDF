// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using BenchmarkDotNet.Attributes;
using HyperPdf.App.Views;
using HyperPdf.Core.Platform;
using HyperPdf.Platform.Linux.Accessibility;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the work every keyboard focus change pays for the screen reader focus repair.</summary>
public class FocusAnnouncementBenchmarks
{
    /// <summary>The event a screen reader registers to hear focus changes.</summary>
    private const string FocusEvent = "object:state-changed:focused";

    /// <summary>The repair under test.</summary>
    private FocusAnnouncementRepair? _repair;

    /// <summary>The focused control's event.</summary>
    private (object Sender, Avalonia.Interactivity.RoutedEventArgs Args) _focus;

    /// <summary>Gets or sets a value indicating whether a screen reader is listening.</summary>
    [Params(false, true)]
    public bool ScreenReader { get; set; }

    /// <summary>Creates the repair and a focus change on a control outside any window.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _repair = new(ScreenReader ? new ListeningCheck() : NoFocusAnnouncementCheck.Instance, static () => []);
        var button = new Button();
        _focus = (button, new FocusChangedEventArgs(InputElement.GotFocusEvent) { NewFocusedElement = button });
    }

    /// <summary>Releases the repair.</summary>
    [GlobalCleanup]
    public void Cleanup() => _repair?.Dispose();

    /// <summary>Handles one focus change up to the point a check would start.</summary>
    [Benchmark]
    public void FocusChange() => _repair!.OnGotFocus(_focus);

    /// <summary>Classifies one registry registration, as each screen reader registration is.</summary>
    /// <returns>Whether the registration makes the bridge send object events.</returns>
    [Benchmark]
    public bool ClassifyRegistration() => AtSpiFocusAnnouncementCheck.IsObjectEvent(FocusEvent);

    /// <summary>A check that reports a listening screen reader and never finds an unheard change.</summary>
    [DebuggerDisplay("ListeningCheck")]
    private sealed class ListeningCheck : IFocusAnnouncementCheck
    {
        /// <inheritdoc/>
        public bool IsActive => true;

        /// <inheritdoc/>
        public AsObservableSignal<RxVoid> Activations { get; } = new(Signal.Never<RxVoid>());

        /// <inheritdoc/>
        public long Announcements => 0;

        /// <inheritdoc/>
        public Task<bool> PrepareRepeatAsync(long announcementsBefore, CancellationToken cancellationToken) => Task.FromResult(false);

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
