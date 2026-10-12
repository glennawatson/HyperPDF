// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Spelling;
using HyperPdf.Platform.Linux.Spelling;
using HyperPdf.Platform.MacOS.Spelling;
using HyperPdf.Platform.Windows.Spelling;

namespace HyperPdf.App.Services;

/// <summary>Chooses the desktop's own spell checker: Windows' and the Mac's built in ones, or the system word lists elsewhere.</summary>
internal static class SpellCheckers
{
    /// <summary>Gets the folder where the reader can add word lists of their own, one word a line, named like en-GB.txt.</summary>
    internal static string DictionaryDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hyperpdf", "dictionaries");

    /// <summary>Creates the spell checker for this desktop and the reader's language.</summary>
    /// <returns>The spell checker; it reports whether a dictionary was found.</returns>
    internal static ISpellChecker Create()
    {
        if (OperatingSystem.IsWindows() && new WindowsSpellChecker() is { IsAvailable: true } windows)
        {
            return windows;
        }

        if (OperatingSystem.IsMacOS() && new MacSpellChecker() is { IsAvailable: true } mac)
        {
            return mac;
        }

        string[] folders = [DictionaryDirectory, .. WordListSpellChecker.SystemFolders];
        return new WordListSpellChecker(WordListSpellChecker.FindWordList(CultureInfo.CurrentUICulture, folders));
    }
}
