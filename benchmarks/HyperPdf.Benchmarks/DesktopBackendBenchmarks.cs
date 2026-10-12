// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.App.Services;

namespace HyperPdf.Benchmarks;

/// <summary>Measures desktop backend selection for Linux sessions.</summary>
public class DesktopBackendBenchmarks
{
    /// <summary>Gets or sets the X11 display offered by the session.</summary>
    [Params(null, ":0")]
    public string? X11Display { get; set; }

    /// <summary>Selects the backend when the session offers Wayland.</summary>
    /// <returns>Whether the app should use native Wayland.</returns>
    [Benchmark]
    public bool SelectBackend() => DesktopBackend.ShouldUseWayland(true, X11Display, "wayland-0");
}
