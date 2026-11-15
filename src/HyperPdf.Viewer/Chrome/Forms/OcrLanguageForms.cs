// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.ViewModels.Preferences;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>Makes the window that lists the languages text can be recognised in.</summary>
internal static class OcrLanguageForms
{
    /// <summary>The rows of the window: what the languages are, one line for each, how a download is going and what can be done.</summary>
    private static readonly FormSetting<OcrLanguagesViewModel>[] Rows =
    [
        FormSettings.Status<OcrLanguagesViewModel>(static m => m.Note),
        FormSettings.Note<OcrLanguagesViewModel>("Press a language to tick it or untick it. A ticked language that is not on this computer downloads at once."),
        FormSettings.Picker<OcrLanguagesViewModel, OcrLanguageItemViewModel>(static m => m.Items, Line, static (m, item) => m.Pick(item)),
        FormSettings.Status<OcrLanguagesViewModel>(static m => m.StatusText).When(static m => m.StatusText.Length > 0),
        FormSettings.Action<OcrLanguagesViewModel>("Stop the download", static m => m.StopDownloadCommand).When(static m => m.IsDownloading),
        FormSettings.Action<OcrLanguagesViewModel>("Remove the downloaded languages I have not ticked", static m => m.RemoveUntickedCommand),
    ];

    /// <summary>Makes the window.</summary>
    /// <param name="model">The view model.</param>
    /// <returns>What the window shows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormRequest For(OcrLanguagesViewModel model) => FormSettings.Window(UiText.Get("Text Recognition Languages"), model, Rows);

    /// <summary>Makes the words of the line of a language: whether it is ticked, its name and whether it is on this computer.</summary>
    /// <param name="item">The language.</param>
    /// <returns>The words.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Line(OcrLanguageItemViewModel item) =>
        UiText.Format(item.IsSelected ? "Ticked: {0}, {1}" : "Not ticked: {0}, {1}", item.Name, item.StatusText);
}
