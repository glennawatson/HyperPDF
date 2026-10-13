// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>Shares parsed ICC transforms among streams opened with one drawing service.</summary>
internal sealed class IccProfileCache
{
    /// <summary>The most profile bytes read.</summary>
    private const int MaxProfileSize = 1 << 25;

    /// <summary>The number of recently loaded profiles shared by content-identical streams.</summary>
    private const int RecentCapacity = 16;

    /// <summary>Guards the recent-profile ring.</summary>
    private readonly Lock _gate = new();

    /// <summary>The latest distinct profile byte contents.</summary>
    private readonly RecentProfile?[] _recent = new RecentProfile?[RecentCapacity];

    /// <summary>The next ring slot to replace.</summary>
    private int _nextRecent;

    /// <summary>Reads a detached profile, caching its result on the stream.</summary>
    /// <param name="stream">The profile stream.</param>
    /// <param name="components">The declared component count.</param>
    /// <returns>The transform, or null when the profile is unusable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IccTransform? ReadUnowned(PdfStream stream, int components) => Get(stream, components, null);

    /// <summary>Gets a transform for a profile stream.</summary>
    /// <param name="stream">The profile stream.</param>
    /// <param name="components">The declared component count.</param>
    /// <returns>The transform, or null when the profile is unusable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IccTransform? Get(PdfStream stream, int components) => Get(stream, components, this);

    /// <summary>Gets or publishes a stream's parsed profile result.</summary>
    /// <param name="stream">The profile stream.</param>
    /// <param name="components">The declared component count.</param>
    /// <param name="cache">The owning services' recent-profile cache, or null for a detached stream.</param>
    /// <returns>The transform, or null when the profile is unusable.</returns>
    private static IccTransform? Get(PdfStream stream, int components, IccProfileCache? cache)
    {
        var transform = stream.CachedIccProfile ?? stream.PublishIccProfile(Load(stream, cache) ?? Unsupported.Instance);
        return transform.Components == components ? transform : null;
    }

    /// <summary>Loads a stream's profile before publishing the result on that stream.</summary>
    /// <param name="stream">The profile stream.</param>
    /// <param name="cache">The owning services' recent-profile cache, or null for a detached stream.</param>
    /// <returns>The transform, or null when the profile is unusable.</returns>
    private static IccTransform? Load(PdfStream stream, IccProfileCache? cache)
    {
        try
        {
            var data = stream.DecodeToArray();
            if (data.Length > MaxProfileSize)
            {
                return null;
            }

            return cache is null ? IccProfile.Create(data) : cache.Shared(data);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Shares a transform with streams containing the same profile bytes.</summary>
    /// <param name="data">The profile bytes.</param>
    /// <returns>The transform, or null when unsupported.</returns>
    private IccTransform? Shared(byte[] data)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.HashData(data, digest);
        var key = new ProfileKey(BinaryPrimitives.ReadUInt128LittleEndian(digest), data.Length);
        lock (_gate)
        {
            foreach (var recent in _recent)
            {
                if (recent?.Key == key)
                {
                    return recent.Transform;
                }
            }
        }

        var entry = new RecentProfile(key, IccProfile.Create(data));
        lock (_gate)
        {
            _recent[_nextRecent] = entry;
            _nextRecent = (_nextRecent + 1) % _recent.Length;
        }

        return entry.Transform;
    }

    /// <summary>Identifies profile bytes by the first 16 digest bytes and length.</summary>
    /// <param name="Hash">The digest prefix.</param>
    /// <param name="Length">The profile length.</param>
    private readonly record struct ProfileKey(UInt128 Hash, int Length);

    /// <summary>The result remembered on a stream whose profile cannot be used. It matches no component count.</summary>
    private sealed class Unsupported : IccTransform
    {
        /// <summary>The component count of the unsupported result: no caller asks for a negative count.</summary>
        private const int NoComponents = -1;

        /// <summary>Gets the shared instance.</summary>
        internal static Unsupported Instance { get; } = new();

        /// <inheritdoc/>
        internal override int Components => NoComponents;

        /// <inheritdoc/>
        internal override void ToRgb(ReadOnlySpan<float> components, Span<float> rgb) =>
            throw new InvalidOperationException("An unusable profile cannot convert colours.");

        /// <inheritdoc/>
        internal override void ConvertRow(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
            throw new InvalidOperationException("An unusable profile cannot convert colours.");
    }

    /// <summary>A recent parsed profile shared across streams.</summary>
    /// <param name="Key">The profile identity.</param>
    /// <param name="Transform">The transform, or null.</param>
    private sealed record RecentProfile(ProfileKey Key, IccTransform? Transform);
}
