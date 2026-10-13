// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Holds the appearances generated for annotations that have none, so a page recorded again (another zoom band, or for
/// printing) reuses them. An entry is the annotation dictionary's identity plus the document's object revision
/// (<see cref="Document.PdfDocumentEditing"/> raises it with every edit), so an edit made in place or through a
/// transaction, an undo and a redo all drop what was drawn from the old objects. It is safe to use from many threads.
/// </summary>
[DebuggerDisplay("GeneratedAppearanceCache: {Count} appearances at revision {_revision}")]
internal sealed class GeneratedAppearanceCache
{
    /// <summary>Guards the maps and the revision.</summary>
    private readonly Lock _gate = new();

    /// <summary>The appearances by annotation identity; null records an annotation that has none to draw.</summary>
    private readonly Dictionary<PdfDictionary, GeneratedAppearance?> _appearances = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>The resource dictionaries shared by appearances with the same opacity and blend mode; made with the first one.</summary>
    private Dictionary<ResourceKey, PdfDictionary>? _resources;

    /// <summary>The object revision the entries were made at.</summary>
    private int _revision;

    /// <summary>How many times <see cref="Clear"/> has run, so an appearance made before a clear is not stored after it.</summary>
    private int _clears;

    /// <summary>Gets the number of cached appearances.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _appearances.Count;
            }
        }
    }

    /// <summary>Gets the appearance of an annotation, making it when it is not cached at the revision.</summary>
    /// <typeparam name="TState">The type of the state given to the factory.</typeparam>
    /// <param name="annotation">The annotation, by identity.</param>
    /// <param name="revision">The object revision read before the annotation was read.</param>
    /// <param name="state">The state given to the factory.</param>
    /// <param name="factory">Makes the appearance; may return null.</param>
    /// <returns>The appearance, or <see langword="null"/> when the annotation draws nothing.</returns>
    internal GeneratedAppearance? GetOrCreate<TState>(PdfDictionary annotation, int revision, TState state, Func<TState, GeneratedAppearance?> factory)
    {
        int clears;
        lock (_gate)
        {
            Align(revision);
            if (revision == _revision && _appearances.TryGetValue(annotation, out var existing))
            {
                return existing;
            }

            clears = _clears;
        }

        var created = factory(state);
        lock (_gate)
        {
            // A revision older than the entries is a render that started before an edit: it draws what it read and caches nothing.
            // The same holds for one that started before a clear, which was made from what the clear dropped.
            Align(revision);
            if (revision != _revision || clears != _clears)
            {
                return created;
            }

            return _appearances.TryAdd(annotation, created) ? created : _appearances[annotation];
        }
    }

    /// <summary>Gets the resources holding a graphics state with an opacity and blend mode, making them when they are new.</summary>
    /// <param name="store">The objects that own the dictionaries.</param>
    /// <param name="stateName">The name the graphics state is selected by.</param>
    /// <param name="lowerCa">The <c>/ca</c> name.</param>
    /// <param name="opacity">The opacity, 0 to 1.</param>
    /// <param name="blend">The blend mode name.</param>
    /// <returns>The resources, which callers never change.</returns>
    internal PdfDictionary GetResources(PdfObjectStore store, PdfName stateName, PdfName lowerCa, float opacity, KnownName blend)
    {
        var key = new ResourceKey(BitConverter.SingleToInt32Bits(opacity), blend);
        lock (_gate)
        {
            if (_resources?.TryGetValue(key, out var existing) == true)
            {
                return existing!;
            }

            // Opacity applies to both stroking (/CA) and other painting (/ca) operations (ISO 32000-2 11.3.7.2).
            var value = PdfValue.FromReal(opacity);
            var state = new PdfDictionary(store);
            state.Set(KnownName.Type, PdfValue.FromName(KnownName.ExtGState));
            state.Set(KnownName.CA, value);
            state.Set(lowerCa, value);
            state.Set(KnownName.BM, PdfValue.FromName(blend));
            var states = new PdfDictionary(store);
            states.Set(stateName, PdfValue.FromDictionary(state));
            var resources = new PdfDictionary(store);
            resources.Set(KnownName.ExtGState, PdfValue.FromDictionary(states));
            (_resources ??= []).Add(key, resources);
            return resources;
        }
    }

    /// <summary>Removes every appearance, for example when fonts they were laid out with change or the document closes.</summary>
    internal void Clear()
    {
        lock (_gate)
        {
            _clears++;
            _appearances.Clear();
            _resources?.Clear();
        }
    }

    /// <summary>Drops the entries when the objects have moved on to a newer revision.</summary>
    /// <param name="revision">The revision the caller read.</param>
    private void Align(int revision)
    {
        if (revision <= _revision)
        {
            return;
        }

        _appearances.Clear();
        _revision = revision;
    }

    /// <summary>The opacity and blend mode a shared resource dictionary holds.</summary>
    /// <param name="OpacityBits">The opacity as its bit pattern, so equal values compare equal.</param>
    /// <param name="Blend">The blend mode.</param>
    private readonly record struct ResourceKey(int OpacityBits, KnownName Blend);
}
