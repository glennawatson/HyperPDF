// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Theming;

namespace HyperPdf.Benchmarks;

/// <summary>Measures selecting a PDF page tone for a light or dark appearance.</summary>
public class ThemeResolverBenchmarks
{
    /// <summary>Gets or sets whether the appearance is dark.</summary>
    [Params(false, true)]
    public bool IsDark { get; set; }

    /// <summary>Selects white or dark pages for the appearance.</summary>
    /// <returns>The page tone.</returns>
    [Benchmark]
    public PageTone SelectPageTone() => ThemeResolver.GetTone(PageToneChoice.FollowDesktop, IsDark ? ColorSchemes.Dark : ColorSchemes.Light);
}
