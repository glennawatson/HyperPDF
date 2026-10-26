// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;

namespace HyperPdf.Benchmarks;

/// <summary>Compares the former English literal with warm generated resource lookup.</summary>
public class UiTextBenchmarks
{
    /// <summary>The English label used by both benchmark paths.</summary>
    private const string Label = "Preferences";

    /// <summary>The original UI culture restored after measurement.</summary>
    private CultureInfo _previous = null!;

    /// <summary>Gets or sets the culture selected before lookup.</summary>
    [Params("en", "fr", "de", "zh-Hans", "ar", "es", "pt", "fr-CA", "es-MX", "pt-BR", "it-IT")]
    public string Culture { get; set; } = "en";

    /// <summary>Warms the resources outside the measured lookup.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _previous = CultureInfo.CurrentUICulture;
        InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo(Culture));
        _ = UiText.Get(Label);
    }

    /// <summary>Returns the original source literal.</summary>
    /// <returns>The English label.</returns>
    [Benchmark(Baseline = true)]
    public string EnglishLiteral() => Label;

    /// <summary>Resolves the same UI label in the selected culture.</summary>
    /// <returns>The translated label.</returns>
    [Benchmark]
    public string GeneratedLookup() => UiText.Get(Label);

    /// <summary>Measures the assembly-independent registry path used by generated XAML bindings.</summary>
    /// <returns>The translated label.</returns>
    [Benchmark]
    public string RegistryLookup() => LocalisationResourceRegistry.TryGetResource("HyperPdf.Localisation.Strings", Label, CultureInfo.CurrentUICulture, out var value)
        ? (string)value!
        : Label;

    /// <summary>Restores the original UI culture.</summary>
    [GlobalCleanup]
    public void Cleanup() => InterfaceCulture.Apply(UiText, _previous);
}
