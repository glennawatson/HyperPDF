// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdf.App.Tests;

/// <summary>Checks startup language precedence without modifying global defaults.</summary>
[Category("Localisation")]
public sealed class InterfaceCultureTests
{
    /// <summary>Explicit language choices override saved preferences and desktop defaults.</summary>
    /// <param name="option">The language option.</param>
    /// <param name="saved">The saved culture.</param>
    /// <param name="expected">The resolved culture.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("--language=de", "fr", "de")]
    [Arguments("--language=zh-Hans", "fr", "zh-Hans")]
    [Arguments("--language=es", "fr", "es")]
    [Arguments("--language=pt", "fr", "pt")]
    [Arguments("--language=pt-BR", "fr", "pt-BR")]
    [Arguments("", "ar", "ar")]
    [Arguments("", "", "en-GB")]
    [Arguments("--language=invalid_culture_!", "fr", "en-GB")]
    public async Task Resolve_AppliesLanguagePrecedence(string option, string saved, string expected)
    {
        var args = string.IsNullOrEmpty(option) ? Array.Empty<string>() : new[] { option };
        var actual = InterfaceCulture.Resolve(args, saved, CultureInfo.GetCultureInfo("en-GB"));
        await Assert.That(actual.Name).IsEqualTo(expected);
    }

    /// <summary>The split command-line spelling consumes its following value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Resolve_SplitOptionOverridesSavedCulture()
    {
        var actual = InterfaceCulture.Resolve(["--language", "ar", "document.pdf"], "fr", CultureInfo.GetCultureInfo("en-GB"));
        await Assert.That(actual.Name).IsEqualTo("ar");
    }

    /// <summary>New language choices preserve existing indexes and expose their saved culture codes.</summary>
    /// <param name="index">The selected language index.</param>
    /// <param name="culture">The persisted culture code.</param>
    /// <param name="label">The language's own name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(6, "es", "Español")]
    [Arguments(7, "pt", "Português")]
    public async Task Choices_ExposeSpanishAndPortuguese(int index, string culture, string label)
    {
        await Assert.That(PreferencesViewModel.GetInterfaceLanguageCode(index)).IsEqualTo(culture);
        await Assert.That(PreferencesViewModel.InterfaceLanguageOptions[index]).IsEqualTo(label);
    }
}
