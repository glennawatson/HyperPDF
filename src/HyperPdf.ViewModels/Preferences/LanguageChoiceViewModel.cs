// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Preferences;

/// <summary>The dialog shown on the first launch: the user picks the interface language, which is kept and applied at once.</summary>
[DebuggerDisplay("LanguageChoiceViewModel: {SelectedIndex}")]
public sealed partial class LanguageChoiceViewModel : ReactiveObject
{
    /// <summary>The application services that hold the settings.</summary>
    private readonly AppServices _services;

    /// <summary>Initializes a new instance of the <see cref="LanguageChoiceViewModel"/> class.</summary>
    /// <param name="services">The application services.</param>
    public LanguageChoiceViewModel(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        SelectedIndex = PreferencesViewModel.GetInterfaceLanguageIndex(services.Settings.InterfaceLanguage);
    }

    /// <summary>Gets the languages, each named in its own language, in the order of <see cref="SelectedIndex"/>.</summary>
    public static IReadOnlyList<string> Options => PreferencesViewModel.InterfaceLanguageOptions;

    /// <summary>Gets or sets the index of the chosen language; 0 follows the desktop.</summary>
    [Reactive]
    public partial int SelectedIndex { get; set; }

    /// <summary>Gets a value indicating whether the dialog should be shown: the user has never chosen a language.</summary>
    /// <param name="services">The application services.</param>
    /// <returns><see langword="true"/> on the first launch.</returns>
    public static bool IsNeeded(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return !services.Settings.InterfaceLanguageAsked && services.Settings.InterfaceLanguage.Length == 0;
    }

    /// <summary>Keeps the chosen language for the next launch and closes the dialog.</summary>
    /// <returns>Always <see langword="true"/>.</returns>
    [ReactiveCommand]
    private bool Continue()
    {
        _services.Settings.InterfaceLanguage = PreferencesViewModel.GetInterfaceLanguageCode(SelectedIndex);
        _services.Settings.InterfaceLanguageAsked = true;
        _services.SaveSettings();
        return true;
    }
}
