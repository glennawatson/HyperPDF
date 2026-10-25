// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Xml.Linq;
using HyperPdf.Scripts;

namespace HyperPdf.App.Tests;

/// <summary>Checks the catalog generator's fallback, override and XML contracts.</summary>
[Category("Localisation")]
public sealed class LocalisationResourcesTests : IDisposable
{
    /// <summary>The source fixture file.</summary>
    private const string CatalogFile = "catalog-a.json";

    /// <summary>The English output file.</summary>
    private const string EnglishResources = "Strings.resx";

    /// <summary>The RESX value element.</summary>
    private const string ValueElement = "value";

    /// <summary>The minimal English catalog.</summary>
    private const string EnglishCatalog = """{"A":"English"}""";

    /// <summary>The count including both cases of a key.</summary>
    private const int CaseSensitiveKeyCount = 2;

    /// <summary>The translated, fallback and empty fixture keys.</summary>
    private const int TranslationKeyCount = 3;

    /// <summary>The isolated directory owned by this test instance.</summary>
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("hyperpdf-localisation-");

    /// <summary>Catalog fragments override earlier files while case-sensitive keys sort ordinally.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Catalogs_PreserveCaseAndOverrideInOrder()
    {
        Write(CatalogFile, """{"a":"lower","A":"original"}""");
        Write("catalog-z.json", """{"A":"override"}""");
        var count = Generate();
        var entries = XDocument.Load(PathFor(EnglishResources)).Root!.Elements("data").ToArray();
        await Assert.That(count).IsEqualTo(CaseSensitiveKeyCount);
        await Assert.That(entries[0].Attribute("name")!.Value).IsEqualTo("A");
        await Assert.That(entries[0].Element(ValueElement)!.Value).IsEqualTo("override");
        await Assert.That(entries[1].Attribute("name")!.Value).IsEqualTo("a");
        await Assert.That(entries[1].Element(ValueElement)!.Value).IsEqualTo("lower");
    }

    /// <summary>Language fragments override the base, missing keys fall back, and null stays empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Translations_PreserveOverridesFallbackAndEmptyValues()
    {
        Write(CatalogFile, """{"A":"English","B":"fallback","C":"not empty"}""");
        Write("translations.fr.json", """{"A":"base","C":null}""");
        Write("translations.fr.a.json", """{"A":"first"}""");
        Write("translations.fr.Z.json", """{"A":"final","extra":"ignored"}""");
        _ = Generate();
        var values = Values("fr");
        await Assert.That(values.Count).IsEqualTo(TranslationKeyCount);
        await Assert.That(values["A"]).IsEqualTo("final");
        await Assert.That(values["B"]).IsEqualTo("fallback");
        await Assert.That(values["C"]).IsEqualTo(string.Empty);
    }

    /// <summary>Fragments alone do not create a language without its base catalog.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingBase_SkipsLanguage()
    {
        Write(CatalogFile, EnglishCatalog);
        Write("translations.fr.extra.json", """{"A":"French"}""");
        _ = Generate();
        await Assert.That(File.Exists(PathFor("Strings.fr.resx"))).IsFalse();
        await Assert.That(File.Exists(PathFor(EnglishResources))).IsTrue();
    }

    /// <summary>Strict failures leave all existing resource files untouched.</summary>
    /// <param name="translation">The base translation, or null when it is absent.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(null)]
    [Arguments("{}")]
    public async Task IncompleteCatalogs_FailBeforeWriting(string? translation)
    {
        Write(CatalogFile, EnglishCatalog);
        Write(EnglishResources, "original");
        if (translation is not null)
        {
            Write("translations.fr.json", translation);
        }

        await Assert.That(() => Generate(true)).Throws<InvalidDataException>();
        await Assert.That(await File.ReadAllTextAsync(PathFor(EnglishResources))).IsEqualTo("original");
    }

    /// <summary>Strict mode generates every supported culture when all keys exist.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompleteCatalogs_GenerateEveryCulture()
    {
        Write(CatalogFile, EnglishCatalog);
        foreach (var culture in new[] { "fr", "de", "zh-Hans", "ar", "es", "pt" })
        {
            Write($"translations.{culture}.json", """{"A":"translated"}""");
        }

        _ = Generate(true);
        foreach (var culture in new[] { "fr", "de", "zh-Hans", "ar", "es", "pt" })
        {
            await Assert.That(Values(culture)["A"]).IsEqualTo("translated");
        }
    }

    /// <summary>The emitted bytes retain escaping, Unicode, LF and BOM-free UTF-8.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Xml_PreservesTheResourceByteFormat()
    {
        Write(CatalogFile, """{"A&B":"é <界>\r\nnext"}""");
        _ = Generate();
        var actual = await File.ReadAllBytesAsync(PathFor(EnglishResources));
        var expected = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n  <data name=\"A&amp;B\" xml:space=\"preserve\">\n    <value>é &lt;界&gt;\nnext</value>\n  </data>\n</root>"u8.ToArray();
        await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>Removes only the unique fixture directory created by this instance.</summary>
    public void Dispose()
    {
        if (_directory.Parent?.FullName == Path.TrimEndingDirectorySeparator(Path.GetTempPath()))
        {
            _directory.Delete(true);
        }
    }

    /// <summary>Runs against the isolated fixture directory.</summary>
    /// <param name="complete">Whether completeness is required.</param>
    /// <returns>The resource key count.</returns>
    private int Generate(bool complete = false) => LocalisationResources.Generate(_directory.FullName, _directory.FullName, complete);

    /// <summary>Writes one fixture catalog or resource.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="text">The fixture content.</param>
    private void Write(string name, string text) => File.WriteAllText(PathFor(name), text);

    /// <summary>Builds a path inside the owned directory.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The absolute fixture path.</returns>
    private string PathFor(string name) => Path.Combine(_directory.FullName, name);

    /// <summary>Reads the generated language's values without interpreting resource keys.</summary>
    /// <param name="culture">The language identifier.</param>
    /// <returns>The case-sensitive resource values.</returns>
    private Dictionary<string, string> Values(string culture)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in XDocument.Load(PathFor($"Strings.{culture}.resx")).Root!.Elements("data"))
        {
            values.Add(entry.Attribute("name")!.Value, entry.Element(ValueElement)!.Value);
        }

        return values;
    }
}
