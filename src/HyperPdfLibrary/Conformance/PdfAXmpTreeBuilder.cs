// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Adds nodes and qualifiers to the XMP data model as the RDF is read, and applies the model's rules: no duplicate
/// properties or qualifiers, <c>rdf:li</c> only in arrays, <c>rdf:value</c> only in structures. A broken rule makes the
/// packet malformed, reported by <see cref="InvalidDataException"/>.
/// </summary>
internal static class PdfAXmpTreeBuilder
{
    /// <summary>The value of <c>rdf:parseType</c> that makes a structure.</summary>
    internal const string ParseTypeResource = "Resource";

    /// <summary>The prefix of namespace declarations.</summary>
    private const string NamespaceDeclaration = "xmlns";

    /// <summary>The non-breaking spaces, which Java's whitespace test excludes.</summary>
    private const string NonBreakingSpaces = "   ";

    /// <summary>The first information separator, which Java counts as whitespace.</summary>
    private const char FirstSeparator = '\u001C';

    /// <summary>The last information separator, which Java counts as whitespace.</summary>
    private const char LastSeparator = '\u001F';

    /// <summary>The language subtag written in upper case, such as the region in <c>en-GB</c>.</summary>
    private const int SecondSubtag = 2;

    /// <summary>Determines whether an attribute declares a namespace.</summary>
    /// <param name="attribute">The attribute.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool IsNamespaceDeclaration(PdfAXmlAttribute attribute) =>
        string.Equals(attribute.Prefix, NamespaceDeclaration, StringComparison.Ordinal)
        || (attribute.Prefix is null && string.Equals(attribute.QualifiedName, NamespaceDeclaration, StringComparison.Ordinal));

    /// <summary>Gets an attribute's name.</summary>
    /// <param name="attribute">The attribute.</param>
    /// <returns>The name.</returns>
    internal static PdfAXmlName NameOf(PdfAXmlAttribute attribute) => new(attribute.Namespace, attribute.Prefix, attribute.LocalName, attribute.QualifiedName);

    /// <summary>Gets an element's name.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The name.</returns>
    internal static PdfAXmlName NameOf(PdfAXmlNode element) => new(element.Namespace, element.Prefix, element.LocalName, element.QualifiedName);

    /// <summary>Determines whether an attribute is <c>xml:lang</c>.</summary>
    /// <param name="attribute">The attribute.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsLanguage(PdfAXmlAttribute attribute) => string.Equals(attribute.QualifiedName, PdfAXmpNode.LanguageQualifier, StringComparison.Ordinal);

    /// <summary>Determines whether an attribute is an RDF attribute with a local name.</summary>
    /// <param name="attribute">The attribute.</param>
    /// <param name="localName">The local name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsRdf(PdfAXmlAttribute attribute, string localName) =>
        string.Equals(attribute.Namespace, PdfAXmpNames.Rdf, StringComparison.Ordinal) && string.Equals(attribute.LocalName, localName, StringComparison.Ordinal);

    /// <summary>Determines whether a node is text made only of whitespace, as Java's <c>Character.isWhitespace</c> sees it.</summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsWhitespace(PdfAXmlNode node)
    {
        if (node.Kind != PdfAXmlNodeKind.Text)
        {
            return false;
        }

        foreach (var character in node.Value)
        {
            if (!IsJavaWhitespace(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the RDF term an element names.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The term.</returns>
    internal static PdfARdfTerm TermOf(PdfAXmlNode node) =>
        node.Kind == PdfAXmlNodeKind.Element && string.Equals(node.Namespace, PdfAXmpNames.Rdf, StringComparison.Ordinal) ? TermOf(node.LocalName) : PdfARdfTerm.Other;

    /// <summary>Gets the RDF term an attribute names; unprefixed <c>about</c> and <c>ID</c> on RDF elements count as RDF.</summary>
    /// <param name="attribute">The attribute.</param>
    /// <param name="owner">The element holding it.</param>
    /// <returns>The term.</returns>
    internal static PdfARdfTerm TermOf(PdfAXmlAttribute attribute, PdfAXmlNode owner)
    {
        var inRdf = string.Equals(attribute.Namespace, PdfAXmpNames.Rdf, StringComparison.Ordinal)
            || (attribute.Namespace is null && attribute.LocalName is "about" or "ID" && string.Equals(owner.Namespace, PdfAXmpNames.Rdf, StringComparison.Ordinal));
        return inRdf ? TermOf(attribute.LocalName) : PdfARdfTerm.Other;
    }

    /// <summary>Adds a node for an element or attribute.</summary>
    /// <param name="tree">The model.</param>
    /// <param name="parent">The parent; ignored for top-level properties, which go to their schema node.</param>
    /// <param name="name">The element or attribute name.</param>
    /// <param name="value">The value.</param>
    /// <param name="isTopLevel">Whether the node is a top-level property.</param>
    /// <returns>The node.</returns>
    /// <exception cref="InvalidDataException">The node breaks a data model rule.</exception>
    internal static PdfAXmpNode AddChild(PdfAXmpTree tree, PdfAXmpNode parent, in PdfAXmlName name, string value, bool isTopLevel)
    {
        var namespaceUri = name.Namespace ?? throw new InvalidDataException("XML namespace required for all elements and attributes.");
        if (string.Equals(namespaceUri, PdfAXmpNames.DublinCoreDeprecated, StringComparison.Ordinal))
        {
            namespaceUri = PdfAXmpNames.DublinCore;
        }

        if (isTopLevel)
        {
            parent = tree.Schema(namespaceUri);
        }

        var isRdf = string.Equals(namespaceUri, PdfAXmpNames.Rdf, StringComparison.Ordinal);
        var isItem = isRdf && string.Equals(name.LocalName, "li", StringComparison.Ordinal);
        var isValue = isRdf && string.Equals(name.LocalName, "value", StringComparison.Ordinal);
        var child = isItem ? new PdfAXmpNode(null, PdfAXmpNode.ItemName, name.Prefix, value) : new PdfAXmpNode(namespaceUri, name.LocalName, name.Prefix, value);
        if (!isItem && parent.Child(namespaceUri, name.LocalName) is not null)
        {
            throw new InvalidDataException("Duplicate property or field node.");
        }

        Place(parent, child, isValue, isItem, isTopLevel);
        return child;
    }

    /// <summary>Adds a qualifier; <c>xml:lang</c> goes first and its value is normalized.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The qualifier name as written.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="InvalidDataException">The node already has the qualifier.</exception>
    internal static void AddQualifier(PdfAXmpNode node, string name, string value)
    {
        foreach (var existing in node.Qualifiers)
        {
            if (string.Equals(existing.Name, name, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Duplicate qualifier.");
            }
        }

        if (string.Equals(name, PdfAXmpNode.LanguageQualifier, StringComparison.Ordinal))
        {
            node.Form |= PdfAXmpForm.Language;
            node.Qualifiers.Insert(0, new(name, NormalizeLanguage(value)));
        }
        else if (string.Equals(name, PdfAXmpNames.TypeQualifier, StringComparison.Ordinal))
        {
            node.Qualifiers.Insert(node.HasLanguage ? 1 : 0, new(name, value));
        }
        else
        {
            node.Qualifiers.Add(new(name, value));
        }
    }

    /// <summary>Folds a structure's <c>rdf:value</c> field into the structure: its value and form become the node's, other fields become qualifiers.</summary>
    /// <param name="node">The structure.</param>
    /// <exception cref="InvalidDataException">Both the structure and its value carry <c>xml:lang</c>, or a qualifier repeats.</exception>
    internal static void FoldValue(PdfAXmpNode node)
    {
        var value = node.Children[0];
        if (value.HasLanguage)
        {
            if (node.HasLanguage)
            {
                throw new InvalidDataException("Redundant xml:lang for rdf:value element.");
            }

            var language = value.Qualifiers[0];
            value.Qualifiers.RemoveAt(0);
            value.Form &= ~PdfAXmpForm.Language;
            AddQualifier(node, language.Name, language.Value);
        }

        foreach (var qualifier in value.Qualifiers)
        {
            AddQualifier(node, qualifier.Name, qualifier.Value);
        }

        for (var i = 1; i < node.Children.Count; i++)
        {
            var field = node.Children[i];
            AddQualifier(node, QualifiedName(field), field.Value);
        }

        node.HasValueChild = false;
        node.Form = (node.Form & ~PdfAXmpForm.Struct) | value.Form;
        node.Value = value.Value;
        node.Children.Clear();
        node.Children.AddRange(value.Children);
    }

    /// <summary>Marks an alternative array as language alternatives when an item has <c>xml:lang</c>, and moves the <c>x-default</c> item first.</summary>
    /// <param name="array">The array.</param>
    internal static void DetectAltText(PdfAXmpNode array)
    {
        if ((array.Form & PdfAXmpForm.Alternate) == 0 || array.Children.Count == 0 || !array.Children.Exists(static item => item.HasLanguage))
        {
            return;
        }

        array.Form |= PdfAXmpForm.AltText;
        var index = array.Children.FindIndex(1, static item => item.Qualifiers.Count > 0 && string.Equals(item.Qualifiers[0].Value, PdfAXmpNames.DefaultLanguage, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        var defaultItem = array.Children[index];
        array.Children.RemoveAt(index);
        array.Children.Insert(0, defaultItem);

        // The XMP toolkit also copies the default text into the item it displaced when that item was first.
        if (index == 1)
        {
            array.Children[1].Value = defaultItem.Value;
        }
    }

    /// <summary>Normalizes an <c>xml:lang</c> value: underscores become hyphens, spaces go, the second subtag is upper case and the rest lower case.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The normalized value.</returns>
    internal static string NormalizeLanguage(string value)
    {
        if (string.Equals(value, PdfAXmpNames.DefaultLanguage, StringComparison.Ordinal))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        var subtag = 1;
        foreach (var character in value)
        {
            switch (character)
            {
                case '-' or '_':
                {
                    _ = builder.Append('-');
                    subtag++;
                    break;
                }

                case ' ':
                {
                    break;
                }

                default:
                {
                    _ = builder.Append(subtag == SecondSubtag ? char.ToUpper(character, CultureInfo.InvariantCulture) : char.ToLower(character, CultureInfo.InvariantCulture));
                    break;
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>Places a new node in its parent, enforcing where <c>rdf:value</c> and <c>rdf:li</c> may appear.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="child">The node.</param>
    /// <param name="isValue">Whether the node is <c>rdf:value</c>.</param>
    /// <param name="isItem">Whether the node is <c>rdf:li</c>.</param>
    /// <param name="isTopLevel">Whether the node is a top-level property.</param>
    /// <exception cref="InvalidDataException">The node is misplaced.</exception>
    private static void Place(PdfAXmpNode parent, PdfAXmpNode child, bool isValue, bool isItem, bool isTopLevel)
    {
        if (!isValue)
        {
            if (isItem && (parent.Form & PdfAXmpForm.Array) == 0)
            {
                throw new InvalidDataException("Misplaced rdf:li element.");
            }

            parent.Children.Add(child);
            return;
        }

        if (isTopLevel || (parent.Form & PdfAXmpForm.Struct) == 0)
        {
            throw new InvalidDataException("Misplaced rdf:value element.");
        }

        parent.Children.Insert(0, child);
        parent.HasValueChild = true;
    }

    /// <summary>Gets a node's name with its prefix, as a qualifier names it.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The name.</returns>
    private static string QualifiedName(PdfAXmpNode node) => node.Prefix is null ? node.Name : $"{node.Prefix}:{node.Name}";

    /// <summary>Gets the RDF term for a local name in the RDF namespace.</summary>
    /// <param name="localName">The local name.</param>
    /// <returns>The term.</returns>
    private static PdfARdfTerm TermOf(string localName) => localName switch
    {
        "li" => PdfARdfTerm.Item,
        "parseType" => PdfARdfTerm.ParseType,
        "Description" => PdfARdfTerm.Description,
        "about" => PdfARdfTerm.About,
        "resource" => PdfARdfTerm.Resource,
        "RDF" => PdfARdfTerm.Rdf,
        "ID" => PdfARdfTerm.Id,
        "nodeID" => PdfARdfTerm.NodeId,
        "datatype" => PdfARdfTerm.Datatype,
        "aboutEach" => PdfARdfTerm.AboutEach,
        "aboutEachPrefix" => PdfARdfTerm.AboutEachPrefix,
        "bagID" => PdfARdfTerm.BagId,
        _ => PdfARdfTerm.Other,
    };

    /// <summary>Determines whether a character is whitespace to Java's <c>Character.isWhitespace</c>.</summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsJavaWhitespace(char character)
    {
        if (character is >= '\t' and <= '\r' or >= FirstSeparator and <= LastSeparator)
        {
            return true;
        }

        var category = char.GetUnicodeCategory(character);
        return category is UnicodeCategory.SpaceSeparator or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
            && !NonBreakingSpaces.Contains(character, StringComparison.Ordinal);
    }
}
