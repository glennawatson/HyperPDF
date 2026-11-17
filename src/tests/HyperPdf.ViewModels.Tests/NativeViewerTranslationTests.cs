// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks that the words of the native viewer's menus, bars and messages are translated in every supported language.</summary>
[NotInParallel]
public sealed class NativeViewerTranslationTests
{
    /// <summary>The fewest of the sample strings that may stay the same as in English; a few words, such as "Oval", are the same in some languages.</summary>
    private const int MostlyTranslated = 2;

    /// <summary>The number of objects the placeholder test puts into a message.</summary>
    private const int Picked = 3;

    /// <summary>Words from the main menu, the tool menus, the bars and the messages, in English.</summary>
    private static readonly string[] Samples =
    [
        "_Print…",
        "_View",
        "Zoom _In",
        "Bring the Picked Object to the Front",
        "Changed how solid it is.",
        "Filled it with a gradient.",
        "Cropped it.",
        "Remove _Hidden Information…",
        "Number the Pages i, ii, iii or A-1…",
        "Pick a picture first.",
        "Read aloud",
        "Type to find",
    ];

    /// <summary>The supported languages other than English.</summary>
    /// <param name="language">The language's culture name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("de")]
    [Arguments("fr")]
    [Arguments("es")]
    [Arguments("pt")]
    [Arguments("ar")]
    [Arguments("zh-Hans")]
    public async Task MenusBarsAndMessagesAreTranslated(string language)
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo(language));
            var unchanged = Samples.Count(static sample => UiText.Get(sample) == sample);
            var mnemonics = Samples.Where(static sample => sample.Contains('_')).All(static sample => UiText.Get(sample).Contains('_'));

            await Assert.That(unchanged).IsLessThanOrEqualTo(MostlyTranslated);
            await Assert.That(mnemonics).IsTrue();
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>A message with a placeholder keeps it in every language, so the number or name still fits in.</summary>
    /// <param name="language">The language's culture name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("de")]
    [Arguments("ar")]
    [Arguments("zh-Hans")]
    public async Task PlaceholdersSurviveTranslation(string language)
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo(language));
            var text = UiText.Format("Picked {0} objects.", Picked);

            await Assert.That(text).Contains("3", StringComparison.Ordinal);
            await Assert.That(text).IsNotEqualTo("Picked 3 objects.");
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }
}
