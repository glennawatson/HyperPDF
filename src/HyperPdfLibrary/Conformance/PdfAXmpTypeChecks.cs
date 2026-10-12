// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Xml.XPath;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks XMP values against value type names, as written in the XMP specification and in PDF/A extension schemas:
/// <c>Bag</c>, <c>Seq</c> and <c>Alt</c> arrays of an item type, <c>Lang Alt</c>, simple types and structures. Names
/// are compared in lower case without "Choice of" wording.
/// </summary>
internal static class PdfAXmpTypeChecks
{
    /// <summary>The type every value matches.</summary>
    private const string Any = "any";

    /// <summary>The language alternative type.</summary>
    private const string LanguageAlternative = "lang alt";

    /// <summary>The text type.</summary>
    private const string Text = "text";

    /// <summary>The unordered array kind.</summary>
    private const string Bag = "bag";

    /// <summary>The alternative array kind.</summary>
    private const string Alt = "alt";

    /// <summary>The ordered array kind.</summary>
    private const string Seq = "seq";

    /// <summary>The array kinds, in the order type names are tested against them.</summary>
    private static readonly string[] ArrayKinds = [Bag, Alt, Seq];

    /// <summary>Determines whether a value matches a type.</summary>
    /// <param name="node">The value.</param>
    /// <param name="typeName">The type name.</param>
    /// <param name="types">The known types.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    internal static bool Matches(PdfAXmpNode node, string typeName, PdfAXmpTypeSet types)
    {
        var type = Simplify(typeName);
        if (string.Equals(type, Any, StringComparison.Ordinal))
        {
            return true;
        }

        return TryArrayKind(type, out var kind, out var itemType)
            ? MatchesArray(node, kind, itemType, types)
            : types.Types.TryGetValue(type, out var known) && MatchesType(node, known);
    }

    /// <summary>Determines whether a type name names a known type, once array kinds are removed.</summary>
    /// <param name="typeName">The type name.</param>
    /// <param name="types">The known types.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool IsKnown(string typeName, PdfAXmpTypeSet types)
    {
        var type = Simplify(typeName);
        while (TryArrayKind(type, out _, out var itemType))
        {
            type = itemType;
        }

        return types.Types.ContainsKey(type);
    }

    /// <summary>Simplifies a type name: lower case, without "open/closed Choice of", and an array kind alone means an array of Text.</summary>
    /// <param name="typeName">The type name.</param>
    /// <returns>The simplified name.</returns>
    internal static string Simplify(string typeName)
    {
        var type = TrimControls(PdfAXmpPatterns.ChoiceWording().Replace(typeName.ToLowerInvariant(), string.Empty));
        if (type.Length == 0)
        {
            return Text;
        }

        if (type.EndsWith(LanguageAlternative, StringComparison.Ordinal))
        {
            return type;
        }

        foreach (var kind in ArrayKinds)
        {
            if (type.EndsWith(kind, StringComparison.Ordinal))
            {
                return $"{type} {Text}";
            }
        }

        return type;
    }

    /// <summary>Splits an array type name, such as <c>bag text</c>, into its kind and item type.</summary>
    /// <param name="type">The simplified type name.</param>
    /// <param name="kind">The array kind.</param>
    /// <param name="itemType">The item type name.</param>
    /// <returns><see langword="true"/> when the name is an array type.</returns>
    private static bool TryArrayKind(string type, out string kind, out string itemType)
    {
        kind = Array.Find(ArrayKinds, candidate => type.Length > candidate.Length && type.StartsWith(candidate, StringComparison.Ordinal) && type[candidate.Length] == ' ') ?? string.Empty;
        itemType = kind.Length == 0 ? type : type[(kind.Length + 1)..];
        return kind.Length > 0;
    }

    /// <summary>Determines whether a value is an array of a kind whose items all match a type.</summary>
    /// <param name="node">The value.</param>
    /// <param name="kind">The array kind.</param>
    /// <param name="itemType">The item type name.</param>
    /// <param name="types">The known types.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool MatchesArray(PdfAXmpNode node, string kind, string itemType, PdfAXmpTypeSet types)
    {
        var form = node.Form;
        var isKind = kind switch
        {
            Alt => (form & PdfAXmpForm.Alternate) != 0,
            Seq => (form & PdfAXmpForm.Ordered) != 0 && (form & PdfAXmpForm.Alternate) == 0,
            _ => (form & PdfAXmpForm.Array) != 0 && (form & (PdfAXmpForm.Ordered | PdfAXmpForm.Alternate)) == 0,
        };
        return isKind && node.Children.TrueForAll(item => Matches(item, itemType, types));
    }

    /// <summary>Determines whether a value matches a known type.</summary>
    /// <param name="node">The value.</param>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool MatchesType(PdfAXmpNode node, PdfAXmpType type) => type.Kind switch
    {
        PdfAXmpTypeKind.Simple => node.IsSimple && (type.Pattern is null || type.Pattern.IsMatch(node.Value)),
        PdfAXmpTypeKind.Date => node.IsSimple && PdfAXmpDates.IsDate(node.Value),
        PdfAXmpTypeKind.LanguageAlternative => (node.Form & PdfAXmpForm.AltText) != 0 || (node.Children.Count == 0 && (node.Form & PdfAXmpForm.Alternate) != 0),
        PdfAXmpTypeKind.XPath => node.IsSimple && CompilesAsXPath(node.Value),
        _ => MatchesStructure(node, type),
    };

    /// <summary>Determines whether a value is a structure whose fields all belong to the type and match their types.</summary>
    /// <param name="node">The value.</param>
    /// <param name="type">The structure type.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool MatchesStructure(PdfAXmpNode node, PdfAXmpType type)
    {
        if ((node.Form & PdfAXmpForm.Struct) == 0 || type.Fields is not { } fields || type.Home is not { } home)
        {
            return false;
        }

        foreach (var field in node.Children)
        {
            if (!fields.TryGetValue(field.Name, out var fieldType)
                || !string.Equals(field.Namespace, type.FieldNamespace, StringComparison.Ordinal)
                || !Matches(field, fieldType, home))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether text compiles as an XPath expression.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool CompilesAsXPath(string text)
    {
        try
        {
            _ = XPathExpression.Compile(text);
            return true;
        }
        catch (XPathException)
        {
            return false;
        }
    }

    /// <summary>Trims leading and trailing characters at or below the space, as Java's <c>String.trim</c> does.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text.</returns>
    private static string TrimControls(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && text[start] <= ' ')
        {
            start++;
        }

        while (end > start && text[end - 1] <= ' ')
        {
            end--;
        }

        return text[start..end];
    }
}
