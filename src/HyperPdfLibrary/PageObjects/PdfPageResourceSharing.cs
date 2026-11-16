// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Makes the resources some content names, such as fonts, pictures and graphics states, available on another page of the same document.</summary>
internal static class PdfPageResourceSharing
{
    /// <summary>The categories of resources a page's content names.</summary>
    private static readonly KnownName[] Categories =
    [
        KnownName.Font, KnownName.XObject, KnownName.ExtGState, KnownName.ColorSpace, KnownName.Pattern, KnownName.Shading,
    ];

    /// <summary>The white space and delimiters of PDF syntax, which end a name.</summary>
    private static readonly SearchValues<byte> Delimiters = SearchValues.Create(" \n\r\t\f\0/<>[]()"u8);

    /// <summary>Makes the resources some content names available on another page, when none of them means something else there.</summary>
    /// <param name="source">The page content the content came from.</param>
    /// <param name="target">The page content the content goes on.</param>
    /// <param name="bytes">The content.</param>
    /// <returns><see langword="true"/> when the resources are available now; <see langword="false"/> when a name clashes, in which case nothing was added.</returns>
    internal static bool TryShare(PdfPageContent source, PdfPageContent target, byte[] bytes)
    {
        var used = Used(source, bytes);
        foreach (var resource in used)
        {
            if (Conflicts(target, resource))
            {
                return false;
            }
        }

        foreach (var resource in used)
        {
            if (!Has(target, resource))
            {
                PdfPageContentEditing.AddResource(target, resource.Category, resource.Name, resource.Value);
            }
        }

        return true;
    }

    /// <summary>Lists the resources of a page that some content names.</summary>
    /// <param name="source">The page content the resources belong to.</param>
    /// <param name="bytes">The content.</param>
    /// <returns>The resources the content names.</returns>
    private static List<ResourceUse> Used(PdfPageContent source, byte[] bytes)
    {
        var used = new List<ResourceUse>();
        var names = source.Document.Objects.Names;
        foreach (var category in Categories)
        {
            if (source.Resources?.GetDictionary(category) is not { } table)
            {
                continue;
            }

            for (var i = 0; i < table.Count; i++)
            {
                var name = table.GetKeyAt(i);
                if (Names(bytes, names.GetSpelling(name)))
                {
                    used.Add(new(category, name, table.GetValueAt(i)));
                }
            }
        }

        return used;
    }

    /// <summary>Tells whether a page already has a resource, in its resources or among those about to be added.</summary>
    /// <param name="page">The page content.</param>
    /// <param name="resource">The resource.</param>
    /// <returns><see langword="true"/> when it has one by that name.</returns>
    private static bool Has(PdfPageContent page, in ResourceUse resource) =>
        page.Resources?.GetDictionary(resource.Category)?.ContainsKey(resource.Name) == true || PdfPageContentEditing.IsPending(page, resource.Category, resource.Name);

    /// <summary>Tells whether a page has a resource by that name that is not the same one.</summary>
    /// <param name="page">The page content.</param>
    /// <param name="resource">The resource.</param>
    /// <returns><see langword="true"/> when the name means something else on the page.</returns>
    private static bool Conflicts(PdfPageContent page, in ResourceUse resource)
    {
        if (PdfPageContentEditing.IsPending(page, resource.Category, resource.Name))
        {
            return true;
        }

        var table = page.Resources?.GetDictionary(resource.Category);
        return table is not null && table.ContainsKey(resource.Name) && !table.GetRaw(resource.Name).Equals(resource.Value);
    }

    /// <summary>Tells whether content names a resource.</summary>
    /// <param name="bytes">The content.</param>
    /// <param name="name">The resource's name, without the slash.</param>
    /// <returns><see langword="true"/> when <c>/name</c> stands in the content.</returns>
    private static bool Names(byte[] bytes, ReadOnlySpan<byte> name)
    {
        var from = 0;
        while (from < bytes.Length)
        {
            var at = bytes.AsSpan(from).IndexOf((byte)'/');
            if (at < 0)
            {
                return false;
            }

            from += at + 1;
            if (bytes.AsSpan(from).StartsWith(name) && EndsName(bytes, from + name.Length))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tells whether a name ends at a place in content.</summary>
    /// <param name="bytes">The content.</param>
    /// <param name="at">The place just after the name's last byte.</param>
    /// <returns><see langword="true"/> at the end of the content, or before white space or a delimiter of PDF syntax.</returns>
    private static bool EndsName(byte[] bytes, int at) =>
        at >= bytes.Length || Delimiters.Contains(bytes[at]);

    /// <summary>A resource a page's content names.</summary>
    /// <param name="Category">The category, such as fonts.</param>
    /// <param name="Name">The name in the category's table.</param>
    /// <param name="Value">The value in the table, without following references.</param>
    private readonly record struct ResourceUse(KnownName Category, PdfName Name, PdfValue Value);
}
