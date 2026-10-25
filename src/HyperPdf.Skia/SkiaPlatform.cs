// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Platform;
using HyperPdf.Skia.Fonts;

namespace HyperPdf.Skia;

/// <summary>Initializes the Skia rendering platform for Avalonia.</summary>
public static class SkiaPlatform
{
    /// <summary>The display DPI used for device-independent coordinates.</summary>
    private const double LogicalDpi = 96;

    /// <summary>Gets the default display DPI.</summary>
    public static Vector DefaultDpi => new(LogicalDpi, LogicalDpi);

    /// <summary>Initializes the Skia platform with default options.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize() => Initialize(new());

    /// <summary>Initializes the Skia platform with the specified options.</summary>
    /// <param name = "options">The Skia configuration options.</param>
    public static void Initialize(SkiaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var renderInterface = new PlatformRenderInterface(options.MaxGpuResourceSizeBytes, options.UseStencilBuffers);
        var locator = AvaloniaLocator.CurrentMutable;
        _ = locator.Bind<SkiaOptions>().ToConstant(options);
        _ = locator.Bind<IPlatformRenderInterface>().ToConstant(renderInterface);
        _ = locator.Bind<IFontManagerImpl>().ToConstant(new FontManagerImpl(options));
    }

    /// <summary>Registers the application's OpenType text shaper.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InitializeTextShaping() =>
        _ = AvaloniaLocator.CurrentMutable.Bind<ITextShaperImpl>().ToConstant(new HarfBuzzTextShaper());
}
