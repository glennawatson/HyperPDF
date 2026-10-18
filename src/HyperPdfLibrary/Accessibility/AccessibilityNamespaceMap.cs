// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Follows the <c>/RoleMapNS</c> chain of a structure type in an explicitly provided namespace (ISO 32000-2 14.8.6) and says
/// whether it breaks ISO 14289-2 8.2.4: a type mapped back to itself, or a chain that returns to a namespace it already passed
/// through with a different type.
/// </summary>
internal static class AccessibilityNamespaceMap
{
    /// <summary>The most namespace role map steps followed.</summary>
    private const int MaxSteps = 32;

    /// <summary>Follows the namespace role maps from a type.</summary>
    /// <param name="space">The namespace dictionary of the type.</param>
    /// <param name="type">The type's spelling.</param>
    /// <param name="cancellationToken">Cancels the namespace walk.</param>
    /// <returns>The problem found; <see cref="NamespaceMapProblem.None"/> when the chain ends or leaves for the default namespace.</returns>
    internal static NamespaceMapProblem Read(PdfDictionary space, string type, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (space.Dict("RoleMapNS")?.Array(type) is not { Count: > 1 } first)
        {
            return NamespaceMapProblem.None;
        }

        var trail = ArrayPool<NamespaceStep>.Shared.Rent(MaxSteps + 1);
        try
        {
            trail[0] = CreateStep(space, space.Key(type));
            return Follow(first, trail, cancellationToken);
        }
        finally
        {
            ArrayPool<NamespaceStep>.Shared.Return(trail, clearArray: true);
        }
    }

    /// <summary>Follows at most the bounded number of role-map entries.</summary>
    /// <param name="first">The first role-map entry.</param>
    /// <param name="trail">The leased visited-step storage.</param>
    /// <param name="cancellationToken">Cancels the namespace walk.</param>
    /// <returns>The problem found, or none when the chain ends.</returns>
    private static NamespaceMapProblem Follow(PdfArray first, Span<NamespaceStep> trail, CancellationToken cancellationToken)
    {
        var target = first;
        for (var step = 0; step < MaxSteps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = target.GetName(0);
            if (name.IsNone || target.GetDictionary(1) is not { } next)
            {
                return NamespaceMapProblem.None;
            }

            // Name IDs are document-local; preserve the bytes when dictionaries have different owners.
            if (target.Owner is { } source && next.Owner is { } owner && source != owner)
            {
                name = owner.Names.Intern(source.Names.GetSpelling(name));
            }

            var current = CreateStep(next, name);
            if (FindRepeat(trail[..(step + 1)], in current, cancellationToken) is { } repeat)
            {
                return repeat;
            }

            trail[step + 1] = current;
            if (next.Dict("RoleMapNS")?.GetArray(name) is not { Count: > 1 } following)
            {
                return NamespaceMapProblem.None;
            }

            target = following;
        }

        return NamespaceMapProblem.Cycle;
    }

    /// <summary>Looks for a namespace the chain has already passed through.</summary>
    /// <param name="trail">The steps so far.</param>
    /// <param name="current">The next namespace and type.</param>
    /// <param name="cancellationToken">Cancels URI decoding.</param>
    /// <returns>The problem, or <see langword="null"/> when the namespace is new.</returns>
    private static NamespaceMapProblem? FindRepeat(ReadOnlySpan<NamespaceStep> trail, in NamespaceStep current, CancellationToken cancellationToken)
    {
        var uri = current.Uri.AsStringBytes();
        foreach (ref readonly var seen in trail)
        {
            if (seen.Unicode || current.Unicode
                ? NamespaceUriEquality.Equal(seen.Uri.AsStringBytes(), uri, cancellationToken)
                : seen.Uri.AsStringBytes().SequenceEqual(uri))
            {
                return SameType(in seen, in current) ? NamespaceMapProblem.Cycle : NamespaceMapProblem.SameNamespace;
            }
        }

        return null;
    }

    /// <summary>Captures whether a URI needs text decoding once per visited namespace.</summary>
    /// <param name="space">The namespace dictionary.</param>
    /// <param name="type">The interned structure type.</param>
    /// <returns>The visited namespace and its URI encoding.</returns>
    private static NamespaceStep CreateStep(PdfDictionary space, PdfName type)
    {
        var uri = space.Value("NS");
        return new(space, type, uri, NamespaceUriEquality.HasUnicodeMark(uri.AsStringBytes()));
    }

    /// <summary>Compares interned types without decoding PDF name bytes.</summary>
    /// <param name="left">The visited type.</param>
    /// <param name="right">The current type.</param>
    /// <returns>Whether both types have the same spelling.</returns>
    private static bool SameType(in NamespaceStep left, in NamespaceStep right) =>
        left.Space.Owner == right.Space.Owner
            ? left.Type == right.Type
            : left.Space.Owner is { } first && right.Space.Owner is { } second
                && first.Names.GetSpelling(left.Type).SequenceEqual(second.Names.GetSpelling(right.Type));

    /// <summary>A type in a namespace on the way through the namespace role maps.</summary>
    /// <param name="Space">The namespace dictionary.</param>
    /// <param name="Type">The interned type.</param>
    /// <param name="Uri">The URI value captured when the namespace is visited.</param>
    /// <param name="Unicode">Whether the URI has a Unicode byte-order mark.</param>
    private readonly record struct NamespaceStep(PdfDictionary Space, PdfName Type, PdfValue Uri, bool Unicode);
}
