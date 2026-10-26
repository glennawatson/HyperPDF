// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Settings;

namespace HyperPdf.App.Services;

/// <summary>Connects application language preferences to the shared localization context.</summary>
internal static class LanguagePreferences
{
    /// <summary>The desktop culture captured before the saved preference is applied.</summary>
    private static readonly CultureInfo DesktopCulture = CultureInfo.CurrentUICulture;

    /// <summary>Applies the startup language preference before controls are constructed.</summary>
    /// <param name="args">The startup arguments.</param>
    internal static void Apply(IReadOnlyList<string> args)
    {
        UiText.Initialize("Hyper PDF Viewer");
        var culture = InterfaceCulture.Resolve(args, new SettingsStore().Load().InterfaceLanguage, DesktopCulture);
        InterfaceCulture.Apply(UiText, culture);
        CP.Localisation.Avalonia.CultureManager.SynchronizeThreadCulture = false;
        CP.Localisation.Avalonia.CultureManager.UICulture = culture;
    }

    /// <summary>Applies the selected language choice to the existing interface.</summary>
    /// <param name="index">The application language choice index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Change(int index) => InterfaceCulture.Apply(
        UiText,
        InterfaceCulture.Resolve([], PreferencesViewModel.GetInterfaceLanguageCode(index), DesktopCulture));
}
