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

    /// <summary>Parsed transforms by stream.</summary>
    private readonly ConditionalWeakTable<PdfStream, Entry> _streams = new();

    /// <summary>The callback reused by the weak table without a delegate per lookup.</summary>
    private readonly ConditionalWeakTable<PdfStream, Entry>.CreateValueCallback _loadEntry;

    /// <summary>Guards the recent-profile ring.</summary>
    private readonly Lock _gate = new();

    /// <summary>The latest distinct profile byte contents.</summary>
    private readonly RecentProfile?[] _recent = new RecentProfile?[RecentCapacity];

    /// <summary>The next ring slot to replace.</summary>
    private int _nextRecent;

    /// <summary>Initializes a new instance of the <see cref="IccProfileCache"/> class.</summary>
    internal IccProfileCache() => _loadEntry = LoadEntry;

    /// <summary>Reads an unowned profile without retaining mutable process state.</summary>
    /// <param name="stream">The profile stream.</param>
    /// <param name="components">The declared component count.</param>
    /// <returns>The transform, or null when the profile is unusable.</returns>
    internal static IccTransform? ReadUnowned(PdfStream stream, int components)
    {
        try
        {
            var data = stream.DecodeToArray();
            var transform = data.Length > MaxProfileSize ? null : IccProfile.Create(data);
            return transform?.Components == components ? transform : null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Gets a transform for a profile stream.</summary>
    /// <param name="stream">The profile stream.</param>
    /// <param name="components">The declared component count.</param>
    /// <returns>The transform, or null when the profile is unusable.</returns>
    internal IccTransform? Get(PdfStream stream, int components)
    {
        var transform = _streams.GetValue(stream, _loadEntry).Transform;
        return transform?.Components == components ? transform : null;
    }

    /// <summary>Creates a weak-table entry for a stream.</summary>
    /// <param name="stream">The profile stream.</param>
    /// <returns>The cached transform, including an unsupported result.</returns>
    private Entry LoadEntry(PdfStream stream)
    {
        try
        {
            var data = stream.DecodeToArray();
            return new(data.Length > MaxProfileSize ? null : Shared(data));
        }
        catch (InvalidDataException)
        {
            return new(null);
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

    /// <summary>A cached stream result.</summary>
    /// <param name="Transform">The transform, or null.</param>
    private sealed record Entry(IccTransform? Transform);

    /// <summary>A recent parsed profile shared across streams.</summary>
    /// <param name="Key">The profile identity.</param>
    /// <param name="Transform">The transform, or null.</param>
    private sealed record RecentProfile(ProfileKey Key, IccTransform? Transform);
}
