// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
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
    /// <returns>The problem found; <see cref="NamespaceMapProblem.None"/> when the chain ends or leaves for the default namespace.</returns>
    internal static NamespaceMapProblem Read(PdfDictionary space, string type)
    {
        if (space.Dict("RoleMapNS")?.Array(type) is not { Count: > 1 } first)
        {
            return NamespaceMapProblem.None;
        }

        var trail = new List<NamespaceStep> { new(space.Text("NS") ?? string.Empty, type) };
        var target = first;
        for (var step = 0; step < MaxSteps; step++)
        {
            if (!TryReadStep(target, out var name, out var next))
            {
                return NamespaceMapProblem.None;
            }

            var uri = next.Text("NS") ?? string.Empty;
            if (FindRepeat(trail, uri, name) is { } repeat)
            {
                return repeat;
            }

            trail.Add(new(uri, name));
            if (next.Dict("RoleMapNS")?.Array(name) is not { Count: > 1 } following)
            {
                return NamespaceMapProblem.None;
            }

            target = following;
        }

        return NamespaceMapProblem.Cycle;
    }

    /// <summary>Reads a role map entry: a type name and the namespace dictionary it belongs to.</summary>
    /// <param name="target">The entry.</param>
    /// <param name="name">Receives the type name.</param>
    /// <param name="space">Receives the namespace dictionary.</param>
    /// <returns><see langword="true"/> when the entry is well formed.</returns>
    private static bool TryReadStep(PdfArray target, [NotNullWhen(true)] out string? name, [NotNullWhen(true)] out PdfDictionary? space)
    {
        name = target.NameText(0);
        space = target.GetDictionary(1);
        return name is not null && space is not null;
    }

    /// <summary>Looks for a namespace the chain has already passed through.</summary>
    /// <param name="trail">The steps so far.</param>
    /// <param name="uri">The namespace of the next step.</param>
    /// <param name="name">The type of the next step.</param>
    /// <returns>The problem, or <see langword="null"/> when the namespace is new.</returns>
    private static NamespaceMapProblem? FindRepeat(List<NamespaceStep> trail, string uri, string name)
    {
        foreach (var seen in trail)
        {
            if (string.Equals(seen.Uri, uri, StringComparison.Ordinal))
            {
                return string.Equals(seen.Type, name, StringComparison.Ordinal) ? NamespaceMapProblem.Cycle : NamespaceMapProblem.SameNamespace;
            }
        }

        return null;
    }

    /// <summary>A type in a namespace on the way through the namespace role maps.</summary>
    /// <param name="Uri">The namespace's URI.</param>
    /// <param name="Type">The type's spelling.</param>
    private readonly record struct NamespaceStep(string Uri, string Type);
}
