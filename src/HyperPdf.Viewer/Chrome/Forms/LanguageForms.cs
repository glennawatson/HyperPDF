// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.Preferences;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>Makes the window that asks for the language of the viewer the first time it starts.</summary>
internal static class LanguageForms
{
    /// <summary>The rows of the window.</summary>
    private static readonly FormSetting<LanguageChoiceViewModel>[] Rows =
    [
        FormSettings.Note<LanguageChoiceViewModel>("Choose the language of the viewer. You can change it later in the preferences."),
        FormSettings.Choice<LanguageChoiceViewModel>("Language", LanguageChoiceViewModel.Options, static m => m.SelectedIndex, static (m, value) => m.SelectedIndex = value),
    ];

    /// <summary>Makes the window.</summary>
    /// <param name="model">The view model that keeps the choice.</param>
    /// <returns>What the window shows.</returns>
    internal static FormRequest For(LanguageChoiceViewModel model) =>
        FormSettings.Window(UiText.Get("Language"), model, Rows) with { AcceptText = UiText.Get("Continue") };
}
