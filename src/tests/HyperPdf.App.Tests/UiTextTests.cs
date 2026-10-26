// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Views;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks generated resources, culture fallback and composite formatting.</summary>
[Category("Localisation")]
public sealed class UiTextTests
{
    /// <summary>The resource key shared by the known translation checks.</summary>
    private const string PreferencesKey = "Preferences";

    /// <summary>Property labels and interface values translate while byte counts and document data remain intact.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Properties_RetainNumbersAndLiteralDataAcrossLanguages()
    {
        var previous = UiText.UICulture;
        using var services = new TestServices();
        using var main = new MainViewModel(services.Services);
        await TestServices.OpenAndWaitAsync(main, [services.CreateDocument("properties.pdf", 1)]);
        PropertiesWindow? window = null;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            var model = new PropertiesViewModel(main.SelectedTab!);
            var size = model.Entries.Single(static entry => entry.Name == "File size");
            var number = size.DisplayValue.Split(' ')[0];
            await Assert.That(size.DisplayValue.StartsWith('0')).IsFalse();
            var literal = new PropertyEntry(LocalizedMessage.Get(UiText, "Author"), LocalizedMessage.Literal(PreferencesKey));
            window = new PropertiesWindow { ViewModel = model };
            window.Show();
            foreach (var language in new[] { "fr", "de", "zh-Hans", "ar", "es", "pt", "en" })
            {
                InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo(language));
                await UiWait.PumpAsync(1);
                await Assert.That(size.DisplayName).IsEqualTo(UiText.Get("File size"));
                await Assert.That(window.Title).IsEqualTo(UiText.Format("Properties — {0}", model.Title));
                await Assert.That(size.DisplayValue.Split(' ')[0]).IsEqualTo(number);
                await Assert.That(literal.DisplayValue).IsEqualTo(PreferencesKey);
                var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(static label => label.Text).ToArray();
                await Assert.That(labels).Contains(size.DisplayName);
            }
        }
        finally
        {
            window?.Close();
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>Work started before a language change uses the application's latest interface language.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_ExistingExecutionContextUsesLatestLanguage()
    {
        var previous = UiText.UICulture;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            var work = Task.Run(async () =>
            {
                entered.SetResult();
                await release.Task;
                return UiText.Get(PreferencesKey);
            });
            await entered.Task;
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("fr"));
            release.SetResult();
            await Assert.That(await work).IsEqualTo("Préférences");
        }
        finally
        {
            _ = release.TrySetResult();
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>Startup registration makes generated resources available before constructing localized controls.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Initialize_RegistersResourcesBeforeLocalizedControls()
    {
        const string titleKey = "Hyper PDF Viewer";
        UiText.Initialize(titleKey);
        await Assert.That(LocalisationResourceRegistry.TryGetResource("HyperPdf.Localisation.Strings", titleKey, CultureInfo.InvariantCulture, out var title)).IsTrue();
        await Assert.That(title).IsEqualTo(titleKey);
        var window = new PreferencesWindow();
        try
        {
            await Assert.That(window.Title).IsEqualTo(UiText.Get(PreferencesKey));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Known translations are selected by the UI culture.</summary>
    /// <param name="culture">The UI culture.</param>
    /// <param name="expected">The translated preference title.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("en", "Preferences")]
    [Arguments("fr", "Préférences")]
    [Arguments("de", "Einstellungen")]
    [Arguments("zh-Hans", "首选项")]
    [Arguments("ar", "التفضيلات")]
    [Arguments("fr-CA", "Préférences")]
    [Arguments("de-AT", "Einstellungen")]
    [Arguments("es", "Preferencias")]
    [Arguments("es-MX", "Preferencias")]
    [Arguments("pt", "Preferências")]
    [Arguments("pt-PT", "Preferências")]
    [Arguments("pt-BR", "Preferências")]
    [Arguments("it-IT", "Preferences")]
    public async Task Get_SelectsTranslationAndParentFallback(string culture, string expected)
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo(culture));
            await Assert.That(UiText.Get(PreferencesKey)).IsEqualTo(expected);
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>Unknown keys keep the useful English source instead of disappearing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_UnknownKeyReturnsSource()
    {
        const string missing = "A future untranslated action";
        await Assert.That(UiText.Get(missing)).IsEqualTo(missing);
    }

    /// <summary>Composite arguments use the user's number culture.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Format_PreservesArgumentsAndCurrentCulture()
    {
        const decimal value = 1234.5M;
        const string first = "first";
        const string second = "second";
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            await Assert.That(UiText.Format("{0:N1}", value)).IsEqualTo(value.ToString("N1", CultureInfo.CurrentCulture));
            await Assert.That(UiText.Format("{0}/{1}", first, second)).IsEqualTo("first/second");
            await Assert.That(UiText.Format("{0}/{1}/{2}", first, second, "third")).IsEqualTo("first/second/third");
            await Assert.That(UiText.Format("{0}/{1}/{2}/{3}", [first, second, "third", "fourth"])).IsEqualTo("first/second/third/fourth");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>An explicit culture lookup does not change the caller's UI culture.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_ExplicitCultureLeavesAmbientCultureUnchanged()
    {
        var previous = UiText.UICulture;
        await Assert.That(UiText.Get(PreferencesKey, CultureInfo.GetCultureInfo("de"))).IsEqualTo("Einstellungen");
        await Assert.That(CultureInfo.CurrentUICulture).IsEqualTo(previous);
    }

    /// <summary>Every translated catalog contains the source keys and keeps its format arguments.</summary>
    /// <param name="culture">The translated catalog.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("fr")]
    [Arguments("de")]
    [Arguments("zh-Hans")]
    [Arguments("ar")]
    [Arguments("es")]
    [Arguments("pt")]
    public async Task Get_TranslatedCatalogKeepsEveryKeyAndFormatArgument(string culture)
    {
        var source = ReadCatalog("Strings.resx");
        var translated = ReadCatalog($"Strings.{culture}.resx");
        await Assert.That(translated.Count).IsEqualTo(source.Count);
        foreach (var entry in source)
        {
            await Assert.That(translated.TryGetValue(entry.Key, out var value)).IsTrue().Because($"{culture}: missing '{entry.Key}'");
            await Assert.That(value).IsNotNullOrEmpty().Because($"{culture}: empty '{entry.Key}'");
            await Assert.That(UiText.Get(entry.Key, CultureInfo.GetCultureInfo(culture))).IsEqualTo(value);
            await Assert.That(CompositeFormat.Parse(value!).MinimumArgumentCount)
                .IsEqualTo(CompositeFormat.Parse(entry.Value).MinimumArgumentCount)
                .Because($"{culture}: format arguments in '{entry.Key}'");
            await AssertArgumentsAsync(entry.Value, value!, culture);
        }
    }

    /// <summary>Every generated XAML resource reference names an existing neutral resource.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Get_EveryGeneratedXamlKeyExistsInCatalog()
    {
        const string keyPrefix = "Key=\"";
        var catalog = ReadCatalog("Strings.resx");
        var directory = Path.Combine(AppContext.BaseDirectory, "LocalisationXaml");
        var checkedKeys = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "*.axaml", SearchOption.AllDirectories))
        {
            foreach (var attribute in XDocument.Load(file).Descendants().Attributes())
            {
                var markup = attribute.Value;
                if (!markup.Contains(":GeneratedResx ", StringComparison.Ordinal))
                {
                    continue;
                }

                var start = markup.LastIndexOf(keyPrefix, StringComparison.Ordinal) + keyPrefix.Length;
                var end = markup.LastIndexOf('"');
                var key = markup[start..end].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
                await Assert.That(catalog.ContainsKey(key)).IsTrue().Because($"{Path.GetFileName(file)}: {attribute.Name}: missing '{key}'");
                checkedKeys++;
            }
        }

        await Assert.That(checkedKeys).IsGreaterThan(0);
    }

    /// <summary>Reads the test-only source catalog copied beside the test binary.</summary>
    /// <param name="fileName">The resource file name.</param>
    /// <returns>The exact ordinal resource keys and values.</returns>
    private static Dictionary<string, string> ReadCatalog(string fileName) => XDocument.Load(Path.Combine(AppContext.BaseDirectory, "LocalisationCatalogs", fileName))
        .Root!.Elements("data").ToDictionary(static element => element.Attribute("name")!.Value, static element => element.Element("value")!.Value, StringComparer.Ordinal);

    /// <summary>Checks argument identity and repetitions even when the greatest argument index is unchanged.</summary>
    /// <param name="source">The English composite format.</param>
    /// <param name="translated">The localized composite format.</param>
    /// <param name="culture">The catalog being checked.</param>
    /// <returns>A task.</returns>
    private static async Task AssertArgumentsAsync(string source, string translated, string culture)
    {
        var count = CompositeFormat.Parse(source).MinimumArgumentCount;
        var arguments = Enumerable.Range(0, count).Select(static index => (object)$"\uE000{index}\uE001").ToArray();
        var sourceText = string.Format(CultureInfo.InvariantCulture, source, arguments);
        var translatedText = string.Format(CultureInfo.InvariantCulture, translated, arguments);
        foreach (var argument in arguments)
        {
            var marker = (string)argument;
            await Assert.That(translatedText.Split(marker).Length)
                .IsEqualTo(sourceText.Split(marker).Length)
                .Because($"{culture}: placeholder identity in '{source}'");
        }
    }
}
