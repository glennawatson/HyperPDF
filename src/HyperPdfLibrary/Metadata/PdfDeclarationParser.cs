// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Xml;

namespace HyperPdfLibrary.Metadata;

/// <summary>Reads PDF Declarations in the packet's existing forward-only XML traversal.</summary>
internal static class PdfDeclarationParser
{
    /// <summary>The declarations property name.</summary>
    internal const string DeclarationsName = "declarations";

    /// <summary>The profile field.</summary>
    private const string ConformsToName = "conformsTo";

    /// <summary>The claim-data array field.</summary>
    private const string ClaimDataName = "claimData";

    /// <summary>The claimant field.</summary>
    private const string ClaimByName = "claimBy";

    /// <summary>The claim date field.</summary>
    private const string ClaimDateName = "claimDate";

    /// <summary>The credentials field.</summary>
    private const string ClaimCredentialsName = "claimCredentials";

    /// <summary>The report URL field.</summary>
    private const string ClaimReportName = "claimReport";

    /// <summary>The RDF namespace.</summary>
    private const string RdfNamespace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    /// <summary>The fixed namespace prefix of declaration property keys.</summary>
    private const string PropertyKeyPrefix = "{http://pdfa.org/declarations/}";

    /// <summary>The depth of a bag item below its property.</summary>
    private const int ItemDepth = 2;

    /// <summary>The structure nesting of a declaration's fields.</summary>
    private const int DeclarationNesting = 1;

    /// <summary>The structure nesting of a claim's fields.</summary>
    private const int ClaimNesting = 2;

    /// <summary>The initial leased array capacity.</summary>
    private const int InitialCapacity = 4;

    /// <summary>The leased array growth factor.</summary>
    private const int GrowthFactor = 2;

    /// <summary>Reads one declarations property, appending only completed entries.</summary>
    /// <param name="reader">The reader on the declarations property.</param>
    /// <param name="properties">The existing flattened property values.</param>
    /// <param name="declarations">The operation's leased declaration storage.</param>
    /// <param name="cancellationToken">Cancels reading and array growth.</param>
    internal static void Read(XmlReader reader, Dictionary<string, string[]> properties, ref Values<PdfDeclaration> declarations, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        XmpParser.ReadAttributes(reader, properties, DeclarationNesting, cancellationToken);
        if (reader.IsEmptyElement)
        {
            return;
        }

        var depth = reader.Depth;
        while (reader.Read() && reader.Depth > depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element && reader.Depth == depth + ItemDepth && IsRdf(reader, "li"))
            {
                Add(ref declarations, ReadDeclaration(reader, properties, cancellationToken), cancellationToken);
            }
        }
    }

    /// <summary>Detaches the collected values into the retained snapshot array.</summary>
    /// <typeparam name="T">The snapshot type.</typeparam>
    /// <param name="values">The collected references.</param>
    /// <param name="cancellationToken">Cancels before copying.</param>
    /// <returns>The owned array, empty when no entry completed.</returns>
    internal static T[] Detach<T>(in Values<T> values, CancellationToken cancellationToken)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        return values.Count == 0 ? [] : values.Buffer.AsSpan(0, values.Count).ToArray();
    }

    /// <summary>Clears and returns operation-owned leased storage.</summary>
    /// <typeparam name="T">The snapshot type.</typeparam>
    /// <param name="values">The leased references.</param>
    internal static void Release<T>(ref Values<T> values)
        where T : class
    {
        if (values.Buffer is { } buffer)
        {
            ArrayPool<T>.Shared.Return(buffer, true);
        }

        values = default;
    }

    /// <summary>Reads a declaration without merging its claim fields with another entry.</summary>
    /// <param name="reader">The reader on an RDF array item.</param>
    /// <param name="properties">The flattened properties.</param>
    /// <param name="cancellationToken">Cancels traversal.</param>
    /// <returns>The completed declaration.</returns>
    private static PdfDeclaration ReadDeclaration(XmlReader reader, Dictionary<string, string[]> properties, CancellationToken cancellationToken)
    {
        var profile = reader.GetAttribute(ConformsToName, XmpMetadata.PdfDeclarationsNamespace);
        var claims = Array.Empty<PdfDeclarationClaim>();
        XmpParser.ReadAttributes(reader, properties, DeclarationNesting, cancellationToken);
        if (reader.IsEmptyElement)
        {
            return new(profile, claims);
        }

        var depth = reader.Depth;
        var fieldDepth = depth + 1;
        while (reader.Read() && reader.Depth > depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.Depth == depth + 1 && IsRdf(reader, "Description"))
            {
                profile = reader.GetAttribute(ConformsToName, XmpMetadata.PdfDeclarationsNamespace) ?? profile;
                XmpParser.ReadAttributes(reader, properties, DeclarationNesting, cancellationToken);
                fieldDepth = reader.Depth + 1;
            }
            else if (reader.Depth == fieldDepth)
            {
                ReadDeclarationField(reader, properties, ref profile, ref claims, cancellationToken);
            }
        }

        return new(profile, claims);
    }

    /// <summary>Reads a declaration field, retaining unknown properties through the general XMP reader.</summary>
    /// <param name="reader">The reader on a field.</param>
    /// <param name="properties">The flattened properties.</param>
    /// <param name="profile">Receives the profile text.</param>
    /// <param name="claims">Receives the associated claim data.</param>
    /// <param name="cancellationToken">Cancels parsing.</param>
    private static void ReadDeclarationField(
        XmlReader reader,
        Dictionary<string, string[]> properties,
        ref string? profile,
        ref PdfDeclarationClaim[] claims,
        CancellationToken cancellationToken)
    {
        if (reader.NamespaceURI != XmpMetadata.PdfDeclarationsNamespace)
        {
            XmpParser.ReadProperty(reader, properties, DeclarationNesting, cancellationToken);
            return;
        }

        switch (reader.LocalName)
        {
            case ConformsToName:
            {
                profile = ReadText(reader, properties, DeclarationNesting, cancellationToken);
                break;
            }

            case ClaimDataName:
            {
                claims = ReadClaims(reader, properties, cancellationToken);
                break;
            }

            default:
            {
                XmpParser.ReadProperty(reader, properties, DeclarationNesting, cancellationToken);
                break;
            }
        }
    }

    /// <summary>Reads the claims belonging to one declaration.</summary>
    /// <param name="reader">The reader on the claim-data property.</param>
    /// <param name="properties">The flattened properties.</param>
    /// <param name="cancellationToken">Cancels reading and copying.</param>
    /// <returns>The declaration's detached claim array.</returns>
    private static PdfDeclarationClaim[] ReadClaims(XmlReader reader, Dictionary<string, string[]> properties, CancellationToken cancellationToken)
    {
        var claims = default(Values<PdfDeclarationClaim>);
        try
        {
            XmpParser.ReadAttributes(reader, properties, ClaimNesting, cancellationToken);
            if (!reader.IsEmptyElement)
            {
                var depth = reader.Depth;
                while (reader.Read() && reader.Depth > depth)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (reader.NodeType == XmlNodeType.Element && reader.Depth == depth + ItemDepth && IsRdf(reader, "li"))
                    {
                        Add(ref claims, ReadClaim(reader, properties, cancellationToken), cancellationToken);
                    }
                }
            }

            return Detach(claims, cancellationToken);
        }
        finally
        {
            Release(ref claims);
        }
    }

    /// <summary>Reads one claim's scalar fields without validating their source values.</summary>
    /// <param name="reader">The reader on an RDF item.</param>
    /// <param name="properties">The flattened properties.</param>
    /// <param name="cancellationToken">Cancels traversal.</param>
    /// <returns>The completed claim.</returns>
    private static PdfDeclarationClaim ReadClaim(XmlReader reader, Dictionary<string, string[]> properties, CancellationToken cancellationToken)
    {
        var fields = ReadClaimAttributes(reader, properties, cancellationToken);
        if (!reader.IsEmptyElement)
        {
            var depth = reader.Depth;
            var fieldDepth = depth + 1;
            while (reader.Read() && reader.Depth > depth)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (reader.Depth == depth + 1 && IsRdf(reader, "Description"))
                {
                    fields = ReadClaimAttributes(reader, properties, cancellationToken);
                    fieldDepth = reader.Depth + 1;
                }
                else if (reader.Depth == fieldDepth)
                {
                    fields = ReadClaimField(reader, properties, fields, cancellationToken);
                }
            }
        }

        return new(fields.ClaimBy, fields.ClaimDate, fields.ClaimCredentials, fields.ClaimReport);
    }

    /// <summary>Reads compact RDF structure attributes.</summary>
    /// <param name="reader">The reader on the structure.</param>
    /// <param name="properties">The flattened properties.</param>
    /// <param name="cancellationToken">Cancels attribute traversal.</param>
    /// <returns>The fields present on this structure.</returns>
    private static ClaimFields ReadClaimAttributes(XmlReader reader, Dictionary<string, string[]> properties, CancellationToken cancellationToken)
    {
        var result = new ClaimFields(
            reader.GetAttribute(ClaimByName, XmpMetadata.PdfDeclarationsNamespace),
            reader.GetAttribute(ClaimDateName, XmpMetadata.PdfDeclarationsNamespace),
            reader.GetAttribute(ClaimCredentialsName, XmpMetadata.PdfDeclarationsNamespace),
            reader.GetAttribute(ClaimReportName, XmpMetadata.PdfDeclarationsNamespace));
        XmpParser.ReadAttributes(reader, properties, ClaimNesting, cancellationToken);
        return result;
    }

    /// <summary>Reads a claim field or delegates an unknown property.</summary>
    /// <param name="reader">The reader on a field.</param>
    /// <param name="properties">The flattened properties.</param>
    /// <param name="fields">The fields collected for this claim.</param>
    /// <param name="cancellationToken">Cancels parsing.</param>
    /// <returns>The fields with this property's value.</returns>
    private static ClaimFields ReadClaimField(XmlReader reader, Dictionary<string, string[]> properties, in ClaimFields fields, CancellationToken cancellationToken)
    {
        var name = reader.LocalName;
        if (reader.NamespaceURI != XmpMetadata.PdfDeclarationsNamespace
            || name is not (ClaimByName or ClaimDateName or ClaimCredentialsName or ClaimReportName))
        {
            XmpParser.ReadProperty(reader, properties, ClaimNesting, cancellationToken);
            return fields;
        }

        var value = ReadText(reader, properties, ClaimNesting, cancellationToken);
        return name switch
        {
            ClaimByName => fields with { ClaimBy = value },
            ClaimDateName => fields with { ClaimDate = value },
            ClaimCredentialsName => fields with { ClaimCredentials = value },
            _ => fields with { ClaimReport = value },
        };
    }

    /// <summary>Reads exact scalar text while maintaining the general parser's trimmed property view.</summary>
    /// <param name="reader">The reader on the field.</param>
    /// <param name="properties">The flattened properties.</param>
    /// <param name="nesting">The structure nesting level.</param>
    /// <param name="cancellationToken">Cancels text traversal.</param>
    /// <returns>The untrimmed text or resource value, including an explicitly empty value.</returns>
    private static string ReadText(XmlReader reader, Dictionary<string, string[]> properties, int nesting, CancellationToken cancellationToken)
    {
        var name = reader.LocalName;
        var resource = reader.GetAttribute("resource", RdfNamespace);
        var text = default(XmpTextValue);
        XmpParser.ReadAttributes(reader, properties, nesting + 1, cancellationToken);
        try
        {
            ReadTextContent(reader, ref text, cancellationToken);
            var value = XmpTextValues.GetText(text);
            var flattened = value.Trim();
            if (flattened.Length == 0 && resource is not null)
            {
                value = resource;
                flattened = resource;
            }

            if (flattened.Length > 0)
            {
                XmpParser.Store(properties, PropertyKey(name), [flattened], true, cancellationToken);
            }

            return value;
        }
        finally
        {
            XmpTextValues.Release(ref text);
        }
    }

    /// <summary>Collects direct scalar text without consuming the following property.</summary>
    /// <param name="reader">The reader on the scalar field.</param>
    /// <param name="text">The collected text.</param>
    /// <param name="cancellationToken">Cancels traversal and copying.</param>
    private static void ReadTextContent(XmlReader reader, ref XmpTextValue text, CancellationToken cancellationToken)
    {
        if (reader.IsEmptyElement)
        {
            return;
        }

        var depth = reader.Depth;
        while (reader.Read() && reader.Depth > depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.Depth == depth + 1 && reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace)
            {
                XmpTextValues.Append(ref text, reader.Value, cancellationToken);
            }
        }
    }

    /// <summary>Appends a snapshot reference without allocating a working list.</summary>
    /// <typeparam name="T">The snapshot type.</typeparam>
    /// <param name="values">The operation-owned array.</param>
    /// <param name="value">The completed snapshot.</param>
    /// <param name="cancellationToken">Cancels before growing storage.</param>
    private static void Add<T>(ref Values<T> values, T value, CancellationToken cancellationToken)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var buffer = values.Buffer;
        if (buffer is null || values.Count == buffer.Length)
        {
            var larger = ArrayPool<T>.Shared.Rent(buffer is null ? InitialCapacity : checked(buffer.Length * GrowthFactor));
            if (buffer is not null)
            {
                buffer.AsSpan(0, values.Count).CopyTo(larger);
                ArrayPool<T>.Shared.Return(buffer, true);
            }

            buffer = larger;
        }

        buffer[values.Count] = value;
        values = new(buffer, values.Count + 1);
    }

    /// <summary>Checks an RDF element by expanded name.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="name">The local name.</param>
    /// <returns>True for the RDF element.</returns>
    private static bool IsRdf(XmlReader reader, string name) => reader.NamespaceURI == RdfNamespace && reader.LocalName == name;

    /// <summary>Reuses fixed property keys instead of constructing them for every field.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>The key used by the generic property view.</returns>
    private static string PropertyKey(string name) => name switch
    {
        ConformsToName => PropertyKeyPrefix + ConformsToName,
        ClaimByName => PropertyKeyPrefix + ClaimByName,
        ClaimDateName => PropertyKeyPrefix + ClaimDateName,
        ClaimCredentialsName => PropertyKeyPrefix + ClaimCredentialsName,
        _ => PropertyKeyPrefix + ClaimReportName,
    };

    /// <summary>The operation's leased reference storage.</summary>
    /// <typeparam name="T">The snapshot type.</typeparam>
    /// <param name="Buffer">The leased array, or null before the first entry.</param>
    /// <param name="Count">The number of completed entries.</param>
    internal readonly record struct Values<T>(T[]? Buffer, int Count)
        where T : class;

    /// <summary>The scalar fields being collected for one claim.</summary>
    /// <param name="ClaimBy">The claimant.</param>
    /// <param name="ClaimDate">The date text.</param>
    /// <param name="ClaimCredentials">The credentials.</param>
    /// <param name="ClaimReport">The report URL.</param>
    private readonly record struct ClaimFields(string? ClaimBy, string? ClaimDate, string? ClaimCredentials, string? ClaimReport);
}
