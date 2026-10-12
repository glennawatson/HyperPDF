// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>An XMP packet as the PDF/A metadata rules see it.</summary>
/// <param name="IsWellFormed">Whether the packet is well-formed XML and valid XMP serialization.</param>
/// <param name="Encoding">The encoding the packet was read in, such as UTF-8, or <see langword="null"/> when it could not be read.</param>
/// <param name="Header">The data of the <c>xpacket</c> header instruction, or <see langword="null"/>.</param>
/// <param name="Properties">The top-level properties, without the extension schema container.</param>
/// <param name="ExtensionSchemas">The <c>pdfaExtension:schemas</c> property, or <see langword="null"/>.</param>
internal sealed record PdfAXmpPacket(bool IsWellFormed, string? Encoding, string? Header, PdfAXmpNode[] Properties, PdfAXmpNode? ExtensionSchemas)
{
    /// <summary>Gets a packet that could not be read.</summary>
    internal static PdfAXmpPacket Malformed { get; } = new(false, null, null, [], null);

    /// <summary>Finds the first top-level property with a namespace and local name.</summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <param name="name">The local name.</param>
    /// <returns>The property, or <see langword="null"/>.</returns>
    internal PdfAXmpNode? Property(string namespaceUri, string name)
    {
        foreach (var property in Properties)
        {
            if (property.Is(namespaceUri, name))
            {
                return property;
            }
        }

        return null;
    }

    /// <summary>Determines whether any top-level property is in a namespace.</summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <returns><see langword="true"/> when one is.</returns>
    internal bool HasNamespace(string namespaceUri)
    {
        foreach (var property in Properties)
        {
            if (string.Equals(property.Namespace, namespaceUri, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets a simple top-level property's value, as the XMP toolkit reads one.</summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <param name="name">The local name.</param>
    /// <param name="value">The value; <see langword="null"/> when the property is absent.</param>
    /// <returns><see langword="false"/> when the property is present but is not simple.</returns>
    internal bool TryGetSimple(string namespaceUri, string name, out string? value)
    {
        var property = Property(namespaceUri, name);
        value = property?.Value;
        return property is null || property.IsSimple;
    }
}
