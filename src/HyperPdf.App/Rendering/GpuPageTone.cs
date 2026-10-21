// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;
using SkiaSharp;

namespace HyperPdf.App.Rendering;

/// <summary>Applies the viewer's page tone to an owned Skia image before tile presentation.</summary>
internal sealed class GpuPageTone : IDisposable
{
    /// <summary>The largest number of reusable tone filters retained for this render hub.</summary>
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

    /// <summary>Protects the entry cache, its in-flight use count and disposal.</summary>
    private readonly Lock _gate = new();

    /// <summary>Entries are immutable and reused by all tiles of the same tone.</summary>
    private readonly Dictionary<PageTone, ToneEntry?> _entries = [];

    /// <summary>The compiled tone effect, shared across colour settings.</summary>
    private SKRuntimeEffect? _effect;

    /// <summary>Whether the native runtime cannot compile the effect.</summary>
    private int _compileFailed;

    /// <summary>Whether the owning render hub has closed.</summary>
    private bool _disposed;

    /// <summary>The number of draws using a cached entry outside the gate.</summary>
    private int _active;

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_active == 0)
            {
                ReleaseResourcesLocked();
            }
        }
    }

    /// <summary>Checks whether a tone has an exact shader implementation on this Skia build.</summary>
    /// <param name="tone">The page tone.</param>
    /// <returns>Whether GPU tone rendering can be attempted.</returns>
    internal bool CanApply(PageTone tone)
    {
        ArgumentNullException.ThrowIfNull(tone);
        if (tone.IsIdentity)
        {
            return true;
        }

        using var lease = AcquireEntry(tone);
        return lease.Value is not null;
    }

    /// <summary>Renders an opaque source image into a toned surface without reading its pixels.</summary>
    /// <param name="source">The untoned tile image.</param>
    /// <param name="info">Its pixel layout and colour space.</param>
    /// <param name="tone">The page tone.</param>
    /// <param name="context">The owning GPU context, or null for raster parity tests.</param>
    /// <returns>An owned toned image, or null when the effect or target is unsupported.</returns>
    internal SKImage? TryApply(SKImage source, SKImageInfo info, PageTone tone, GRContext? context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(tone);

        // The gate covers only the cache lookup. The lease keeps the entry alive while this draw runs outside it.
        using var lease = AcquireEntry(tone);
        if (lease.Value is not { } entry)
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
            surface.Canvas.DrawImage(source, new SKRect(0, 0, info.Width, info.Height), new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), entry.Paint);
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

    /// <summary>Writes one tone's channel values into an immutable colour filter and the paint that carries it.</summary>
    /// <param name="effect">The compiled effect.</param>
    /// <param name="tone">The tone.</param>
    /// <returns>The entry, or null when the effect gave no filter.</returns>
    private static ToneEntry? CreateEntry(SKRuntimeEffect effect, PageTone tone)
    {
        using var uniforms = new SKRuntimeEffectUniforms(effect);
        uniforms["paper"] = Color(tone.Paper);
        uniforms["ink"] = Color(tone.Ink);
        var filter = effect.ToColorFilter(uniforms);
        return filter is null ? null : new(filter, new SKPaint { ColorFilter = filter, BlendMode = SKBlendMode.Src });
    }

    /// <summary>Converts a packed RGB colour into shader channel values.</summary>
    /// <param name="rgb">The packed colour.</param>
    /// <returns>The channel values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKPoint3 Color(uint rgb) => new((rgb >> RedShift) & ChannelMax, (rgb >> GreenShift) & ChannelMax, rgb & ChannelMax);

    /// <summary>Gets a cached entry or creates a short-lived entry after the cache fills.</summary>
    /// <param name="tone">The tone.</param>
    /// <returns>The entry lease.</returns>
    private ToneLease AcquireEntry(PageTone tone)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return default;
            }

            if (_entries.TryGetValue(tone, out var existing))
            {
                return existing is null ? default : LeaseCachedLocked(existing);
            }

            var effect = GetEffectLocked();
            if (effect is null)
            {
                return default;
            }

            ToneEntry? entry;
            try
            {
                entry = CreateEntry(effect, tone);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or SKRuntimeEffectBuilderException)
            {
                entry = null;
            }

            if (entry is null)
            {
                if (_entries.Count < CachedTones)
                {
                    _entries.Add(tone, null);
                }

                return default;
            }

            if (_entries.Count >= CachedTones)
            {
                return new(entry, null);
            }

            _entries.Add(tone, entry);
            return LeaseCachedLocked(entry);
        }
    }

    /// <summary>Marks a cached entry as in use so disposal waits for the draw.</summary>
    /// <param name="entry">The cached entry.</param>
    /// <returns>The lease.</returns>
    private ToneLease LeaseCachedLocked(ToneEntry entry)
    {
        _active++;
        return new(entry, this);
    }

    /// <summary>Ends a cached entry's use and frees the cache if the hub closed meanwhile.</summary>
    private void ReleaseCached()
    {
        lock (_gate)
        {
            _active--;
            if (_disposed && _active == 0)
            {
                ReleaseResourcesLocked();
            }
        }
    }

    /// <summary>Disposes every cached entry and the compiled effect.</summary>
    private void ReleaseResourcesLocked()
    {
        foreach (var (_, entry) in _entries)
        {
            entry?.Dispose();
        }

        _entries.Clear();
        _effect?.Dispose();
        _effect = null;
    }

    /// <summary>Compiles the effect once while the cache gate is held.</summary>
    /// <returns>The effect or null when unsupported.</returns>
    private SKRuntimeEffect? GetEffectLocked()
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

    /// <summary>Keeps a cached entry alive for one draw, or owns an entry made after the cache filled.</summary>
    /// <param name="Value">The entry, or null when unsupported.</param>
    /// <param name="Cache">The cache that counts this use, or null when the lease owns the entry.</param>
    private readonly record struct ToneLease(ToneEntry? Value, GpuPageTone? Cache) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            if (Cache is not null)
            {
                Cache.ReleaseCached();
                return;
            }

            Value?.Dispose();
        }
    }

    /// <summary>A tone's colour filter and the reusable paint that carries it.</summary>
    /// <param name="Filter">The immutable colour filter.</param>
    /// <param name="Paint">The paint, which draws never change.</param>
    private sealed record ToneEntry(SKColorFilter Filter, SKPaint Paint) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            Paint.Dispose();
            Filter.Dispose();
        }
    }
}
