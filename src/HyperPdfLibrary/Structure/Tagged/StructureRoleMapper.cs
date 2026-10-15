// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Resolves an element's type to a standard type through the root <c>/RoleMap</c> and, for PDF 2.0 namespaces, each
/// namespace's <c>/RoleMapNS</c>. The role map is followed even for a type that is already standard, because a document may
/// remap a standard name (ISO 32000-2 14.7.3). Chains are followed to their end with a step limit, so a loop in the maps stops.
/// </summary>
[DebuggerDisplay("StructureRoleMapper")]
internal sealed class StructureRoleMapper
{
    /// <summary>The most role map steps followed for one type.</summary>
    private const int MaxSteps = 16;

    /// <summary>The interned structure names.</summary>
    private readonly TaggedNames _names;

    /// <summary>The document's name table.</summary>
    private readonly PdfNameTable _table;

    /// <summary>The root role map, or <see langword="null"/>.</summary>
    private readonly PdfDictionary? _roleMap;

    /// <summary>The resolved types of the default namespace by name id, since most documents use a few types many times.</summary>
    private readonly Dictionary<int, ResolvedType> _defaults = [];

    /// <summary>The namespace URIs by namespace dictionary.</summary>
    private readonly Dictionary<PdfDictionary, string> _uris = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>Initializes a new instance of the <see cref="StructureRoleMapper"/> class.</summary>
    /// <param name="names">The interned structure names.</param>
    /// <param name="table">The document's name table.</param>
    /// <param name="roleMap">The structure tree root's <c>/RoleMap</c>, or <see langword="null"/>.</param>
    internal StructureRoleMapper(TaggedNames names, PdfNameTable table, PdfDictionary? roleMap)
    {
        _names = names;
        _table = table;
        _roleMap = roleMap;
    }

    /// <summary>Resolves an element's type.</summary>
    /// <param name="type">The element's <c>/S</c>.</param>
    /// <param name="space">The element's <c>/NS</c> namespace dictionary, or <see langword="null"/> for the default namespace.</param>
    /// <returns>The standard type and the name it was reached through.</returns>
    internal ResolvedType Resolve(PdfName type, PdfDictionary? space)
    {
        if (space is null)
        {
            ref var cached = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(_defaults, type.Id, out var exists);
            if (!exists)
            {
                cached = ResolveChain(type, null);
            }

            return cached;
        }

        return ResolveChain(type, space);
    }

    /// <summary>Gets a namespace's URI.</summary>
    /// <param name="space">The namespace dictionary.</param>
    /// <returns>The URI; empty when it has none.</returns>
    internal string GetUri(PdfDictionary space)
    {
        ref var uri = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(_uris, space, out var exists);
        if (!exists)
        {
            uri = space.GetText(_names.NS) ?? string.Empty;
        }

        return uri!;
    }

    /// <summary>Follows the role maps from a type to a standard one.</summary>
    /// <param name="type">The type.</param>
    /// <param name="space">The namespace, or <see langword="null"/> for the default.</param>
    /// <returns>The resolved type.</returns>
    private ResolvedType ResolveChain(PdfName type, PdfDictionary? space)
    {
        for (var step = 0; step < MaxSteps; step++)
        {
            var standard = PdfStructureTypes.Find(_table.GetSpelling(type));
            var flags = space is null ? PdfStructureTypes.AnyStandardFlag : PdfStructureTypes.GetNamespaceFlags(GetUri(space));
            if (!MapOnce(ref type, ref space, standard, flags))
            {
                return Finish(type, standard, flags, space);
            }
        }

        return new(PdfStructureType.Unknown, _table.GetString(type), false);
    }

    /// <summary>Makes the result once no map applies.</summary>
    /// <param name="type">The last type reached.</param>
    /// <param name="standard">Its standard type, if any.</param>
    /// <param name="flags">The namespaces it is in.</param>
    /// <param name="space">The namespace it is in, or <see langword="null"/> for the default.</param>
    /// <returns>The resolved type.</returns>
    private ResolvedType Finish(PdfName type, PdfStructureType standard, int flags, PdfDictionary? space)
    {
        var known = flags != 0 && PdfStructureTypes.IsStandardIn(standard, flags) ? standard : PdfStructureType.Unknown;
        return new(known, _table.GetString(type), space is not null && string.Equals(GetUri(space), PdfStructureTypes.MathMlUri, StringComparison.Ordinal));
    }

    /// <summary>Takes one role map step.</summary>
    /// <param name="type">The type; receives the mapped type.</param>
    /// <param name="space">The namespace; receives the mapped type's namespace.</param>
    /// <param name="standard">The type's standard type, if any.</param>
    /// <param name="flags">The namespace's standard flags; zero for another namespace.</param>
    /// <returns><see langword="true"/> when a map applied.</returns>
    private bool MapOnce(ref PdfName type, ref PdfDictionary? space, PdfStructureType standard, int flags)
    {
        if (space is null)
        {
            if (_roleMap?.Get(type).TryGetName(out var next) != true || next == type)
            {
                return false;
            }

            type = next;
            return true;
        }

        // A standard type in a standard namespace is final; PDF 2.0 forbids remapping it.
        return (flags == 0 || !PdfStructureTypes.IsStandardIn(standard, flags)) && MapInNamespace(ref type, ref space);
    }

    /// <summary>Takes one step through a namespace's <c>/RoleMapNS</c>.</summary>
    /// <param name="type">The type; receives the mapped type.</param>
    /// <param name="space">The namespace; receives the mapped type's namespace.</param>
    /// <returns><see langword="true"/> when a map applied.</returns>
    private bool MapInNamespace(ref PdfName type, ref PdfDictionary? space)
    {
        var target = space?.GetDictionary(_names.RoleMapNS)?.Get(type) ?? default;
        if (target.TryGetName(out var mapped))
        {
            // A bare name maps into the default (PDF 1.7) namespace.
            type = mapped;
            space = null;
            return true;
        }

        if (target.AsArray() is { Count: > 0 } pair && pair.Get(0).TryGetName(out mapped))
        {
            type = mapped;
            space = pair.GetDictionary(1);
            return true;
        }

        return false;
    }
}
