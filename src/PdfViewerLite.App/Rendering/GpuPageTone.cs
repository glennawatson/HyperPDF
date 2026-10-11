// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Rendering;
using SkiaSharp;

namespace PdfViewerLite.App.Rendering;

/// <summary>Applies the viewer's page tone to an owned Skia image before tile presentation.</summary>
internal static class GpuPageTone
{
    /// <summary>The largest number of reusable tone filters retained for the process.</summary>
    private const int CachedTones = 8;

    /// <summary>The number of values in an eight-bit colour channel.</summary>
    private const int ChannelValues = 256;

    /// <summary>The highest eight-bit colour value.</summary>
    private const int ChannelMax = ChannelValues - 1;

    /// <summary>The shift from the red channel to the green channel.</summary>
    private const int RedShift = 16;

    /// <summary>The shift from the green channel to the blue channel.</summary>
    private const int GreenShift = 8;

    /// <summary>The exact integer tone mapping used by PageTone.Apply, expressed in SkSL.</summary>
    private const string ToneShader = """
        uniform float3 paper;
        uniform float3 ink;

        half4 main(half4 color) {
            float3 rgb = floor(float3(color.rgb) * 255.0 + 0.5);
            float luma = floor((rgb.b * 29.0 + rgb.g * 150.0 + rgb.r * 77.0) / 256.0);
            float3 sum = ink * (255.0 - luma) + paper * luma + 128.0;
            float3 toned = floor((sum + floor(sum / 256.0)) / 256.0);
            float3 mapped = clamp(toned + rgb - luma, 0.0, 255.0);
            return half4(half3(mapped / 255.0), color.a);
        }
        """;

    /// <summary>Protects filter creation and the bounded process cache.</summary>
    private static readonly Lock Gate = new();

    /// <summary>Filters are immutable and reused by all tiles of the same tone.</summary>
    private static readonly Dictionary<PageTone, SKColorFilter?> Filters = [];

    /// <summary>The compiled tone effect, shared across colour settings.</summary>
    private static SKRuntimeEffect? _effect;

    /// <summary>Whether the native runtime cannot compile the effect.</summary>
    private static int _compileFailed;

    /// <summary>Checks whether a tone has an exact shader implementation on this Skia build.</summary>
    /// <param name="tone">The page tone.</param>
    /// <returns>Whether GPU tone rendering can be attempted.</returns>
    internal static bool CanApply(PageTone tone)
    {
        ArgumentNullException.ThrowIfNull(tone);
        if (tone.IsIdentity)
        {
            return true;
        }

        using var filter = AcquireFilter(tone);
        return filter.Value is not null;
    }

    /// <summary>Renders an opaque source image into a toned surface without reading its pixels.</summary>
    /// <param name="source">The untoned tile image.</param>
    /// <param name="info">Its pixel layout and colour space.</param>
    /// <param name="tone">The page tone.</param>
    /// <param name="context">The owning GPU context, or null for raster parity tests.</param>
    /// <returns>An owned toned image, or null when the effect or target is unsupported.</returns>
    internal static SKImage? TryApply(SKImage source, SKImageInfo info, PageTone tone, GRContext? context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(tone);
        using var filter = AcquireFilter(tone);
        if (filter.Value is null)
        {
            return null;
        }

        using var surface = TryCreateSurface(context, info);
        if (surface is null)
        {
            return null;
        }

        try
        {
            using var paint = new SKPaint { ColorFilter = filter.Value, BlendMode = SKBlendMode.Src };
            surface.Canvas.DrawImage(source, new SKRect(0, 0, info.Width, info.Height), new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), paint);
            return surface.Snapshot();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>Creates a GPU or raster surface with the supplied format.</summary>
    /// <param name="context">The graphics context, or null for raster output.</param>
    /// <param name="info">The target layout.</param>
    /// <returns>The surface or null.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SKSurface? TryCreateSurface(GRContext? context, SKImageInfo info)
    {
        try
        {
            return context is null ? SKSurface.Create(info) : SKSurface.Create(context, false, info);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>Gets a cached filter or creates a short-lived filter after the cache fills.</summary>
    /// <param name="tone">The tone.</param>
    /// <returns>The filter lease.</returns>
    private static ToneFilterLease AcquireFilter(PageTone tone)
    {
        lock (Gate)
        {
            if (Filters.TryGetValue(tone, out var existing))
            {
                return new(existing, false);
            }

            var effect = GetEffectLocked();
            if (effect is null)
            {
                return default;
            }

            SKColorFilter? filter;
            try
            {
                filter = CreateFilter(effect, tone);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or SKRuntimeEffectBuilderException)
            {
                if (Filters.Count < CachedTones)
                {
                    Filters.Add(tone, null);
                }

                return default;
            }

            if (Filters.Count >= CachedTones)
            {
                return new(filter, true);
            }

            Filters.Add(tone, filter);
            return new(filter, false);
        }
    }

    /// <summary>Compiles the effect once while the cache gate is held.</summary>
    /// <returns>The effect or null when unsupported.</returns>
    private static SKRuntimeEffect? GetEffectLocked()
    {
        if (Volatile.Read(ref _compileFailed) != 0)
        {
            return null;
        }

        try
        {
            _effect ??= SKRuntimeEffect.CreateColorFilter(ToneShader, out _);
        }
        catch (InvalidOperationException)
        {
            _ = Interlocked.Exchange(ref _compileFailed, 1);
            return null;
        }

        _ = Interlocked.Exchange(ref _compileFailed, _effect is null ? 1 : 0);
        return _effect;
    }

    /// <summary>Writes one tone's channel values into an immutable colour filter.</summary>
    /// <param name="effect">The compiled effect.</param>
    /// <param name="tone">The tone.</param>
    /// <returns>The filter.</returns>
    private static SKColorFilter? CreateFilter(SKRuntimeEffect effect, PageTone tone)
    {
        using var uniforms = new SKRuntimeEffectUniforms(effect);
        uniforms["paper"] = Color(tone.Paper);
        uniforms["ink"] = Color(tone.Ink);
        return effect.ToColorFilter(uniforms);
    }

    /// <summary>Converts a packed RGB colour into shader channel values.</summary>
    /// <param name="rgb">The packed colour.</param>
    /// <returns>The channel values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKPoint3 Color(uint rgb) => new((rgb >> RedShift) & ChannelMax, (rgb >> GreenShift) & ChannelMax, rgb & ChannelMax);

    /// <summary>Owns only filters created after the bounded shared cache fills.</summary>
    /// <param name="Value">The filter, or null when unsupported.</param>
    /// <param name="Owns">Whether this use must release the filter.</param>
    private readonly record struct ToneFilterLease(SKColorFilter? Value, bool Owns) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            if (Owns)
            {
                Value?.Dispose();
            }
        }
    }
}
