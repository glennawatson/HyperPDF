// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using HyperPdfLibrary.Editing;

namespace HyperPdfLibrary.Metadata;

/// <summary>
/// Writes a PDF Declaration (PDF Association, PDF Declarations, 2019-09) into an XMP packet: one <c>rdf:li</c> structure in the
/// <c>pdfd:declarations</c> bag, with <c>pdfd:conformsTo</c> (Table 2) and the optional claim fields (Table 3). Fields are checked
/// before anything is written, so a value the specification does not allow never reaches the file.
/// </summary>
internal static class PdfDeclarationWriter
{
    /// <summary>The preferred prefix of the declarations schema (clause 7.1).</summary>
    private const string PreferredPrefix = "pdfd";

    /// <summary>The start of the fragment that names a report embedded in the file (clause 8.2.2, Table 3 note).</summary>
    private const string EmbeddedReportFragment = "#ef=";

    /// <summary>The longest text a field may hold.</summary>
    private const int MaxFieldLength = 2048;

    /// <summary>The formats an XMP date may take (ISO 8601 profile of ISO 16684-1).</summary>
    private static readonly string[] DateFormats =
    [
        "yyyy",
        "yyyy-MM",
        "yyyy-MM-dd",
        "yyyy-MM-dd'T'HH:mmzzz",
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
        "yyyy-MM-dd'T'HH:mm'Z'",
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
    ];

    /// <summary>Gets the PDF/A extension schema that describes the <c>pdfd:declarations</c> property, which PDF/A files need before they can hold a declaration (PDF Declarations, clause 9).</summary>
    internal static XmpExtensionProperty ExtensionSchema { get; } = new(
        "PDF Declarations",
        "http://pdfa.org/declarations/",
        PreferredPrefix,
        "declarations",
        "Bag Declaration",
        "Declarations that the file conforms to other standards or profiles.")
    {
        ValueTypes =
        [
            new(
                "Declaration",
                "One declaration: the profile the file claims to meet and the claims behind it.",
                [
                    new("conformsTo", "URI", "The address of the standard or profile."),
                    new("claimData", "Bag Claim", "Details of who claimed it and when."),
                ]),
            new(
                "Claim",
                "The details of one conformance claim.",
                [
                    new("claimBy", "Text", "Who made the claim."),
                    new("claimDate", "Date", "When the claim was made."),
                    new("claimCredentials", "Text", "The claimant's credentials."),
                    new("claimReport", "URI", "A report on the claim."),
                ]),
        ],
    };

    /// <summary>Checks a declaration against the specification's tables.</summary>
    /// <param name="declaration">The declaration.</param>
    /// <exception cref="ArgumentException">A field is missing or not allowed.</exception>
    internal static void Validate(PdfDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (declaration.ConformsTo is not { Length: > 0 } profile || !IsAbsoluteUri(profile))
        {
            throw new ArgumentException("A declaration needs the address of the standard or profile it names, such as https://example.org/profile.", nameof(declaration));
        }

        foreach (var claim in declaration.ClaimData)
        {
            CheckText(claim.ClaimBy, "claimant");
            CheckText(claim.ClaimCredentials, "credentials");
            if (claim.ClaimDate is { } date && !IsXmpDate(date))
            {
                throw new ArgumentException($"'{date}' is not a date. Use a form such as 2026-10-19 or 2026-10-19T09:30:00+10:00.", nameof(declaration));
            }

            if (claim.ClaimReport is { } report && !IsAbsoluteUri(report) && !IsEmbeddedReport(report))
            {
                throw new ArgumentException("A report must be a web address, or #ef= followed by the name of a file embedded in the document.", nameof(declaration));
            }
        }
    }

    /// <summary>Adds a declaration to a UTF-8 packet.</summary>
    /// <param name="packet">The packet's bytes, UTF-8 and well formed.</param>
    /// <param name="declaration">The declaration, already validated.</param>
    /// <returns>The new packet; <see langword="null"/> when the packet cannot take the declaration safely, or already holds it.</returns>
    internal static byte[]? Add(byte[] packet, PdfDeclaration declaration)
    {
        var prefix = XmpEditor.FindPrefix(packet, "http://pdfa.org/declarations/"u8) is { } found ? Encoding.UTF8.GetString(found) : PreferredPrefix;
        var rdf = XmpEditor.FindPrefix(packet, "http://www.w3.org/1999/02/22-rdf-syntax-ns#"u8) is { } bound ? Encoding.UTF8.GetString(bound) : "rdf";
        var text = Encoding.UTF8.GetString(packet);
        var item = BuildItem(prefix, rdf, declaration);
        var opening = text.IndexOf($"<{prefix}:declarations", StringComparison.Ordinal);
        var edited = opening < 0 ? AddProperty(text, prefix, rdf, item) : AddToBag(text, opening, prefix, rdf, item);
        if (edited is null)
        {
            return null;
        }

        var result = Encoding.UTF8.GetBytes(edited);
        return XmpEditor.IsWellFormed(result) ? result : null;
    }

    /// <summary>Removes the declaration that names a profile from a UTF-8 packet, leaving every other declaration as it was.</summary>
    /// <param name="packet">The packet's bytes, UTF-8 and well formed.</param>
    /// <param name="conformsTo">The profile address.</param>
    /// <returns>The new packet; <see langword="null"/> when no declaration names the profile or it cannot be cut out safely.</returns>
    internal static byte[]? Remove(byte[] packet, string conformsTo)
    {
        var rdf = XmpEditor.FindPrefix(packet, "http://www.w3.org/1999/02/22-rdf-syntax-ns#"u8) is { } bound ? Encoding.UTF8.GetString(bound) : "rdf";
        var text = Encoding.UTF8.GetString(packet);
        var hit = text.IndexOf($":conformsTo>{SecurityElement.Escape(conformsTo)}<", StringComparison.Ordinal);
        var start = hit < 0 ? -1 : text.LastIndexOf($"<{rdf}:li", hit, StringComparison.Ordinal);
        var end = start < 0 ? -1 : FindItemEnd(text, start, rdf);
        if (end < 0 || end < hit)
        {
            return null;
        }

        var result = Encoding.UTF8.GetBytes(text.Remove(start, end - start));
        return XmpEditor.IsWellFormed(result) ? result : null;
    }

    /// <summary>Determines whether a packet already declares conformance to a profile.</summary>
    /// <param name="packet">The packet's bytes.</param>
    /// <param name="conformsTo">The profile address.</param>
    /// <returns><see langword="true"/> when an element holds exactly that address.</returns>
    internal static bool Holds(ReadOnlySpan<byte> packet, string conformsTo) =>
        packet.IndexOf(Encoding.UTF8.GetBytes($":conformsTo>{SecurityElement.Escape(conformsTo)}<")) >= 0;

    /// <summary>Determines whether the packet names the declarations schema in a PDF/A extension schema, which PDF/A files need (clause 9).</summary>
    /// <param name="packet">The packet's bytes.</param>
    /// <returns><see langword="true"/> when a <c>namespaceURI</c> element holds the schema's address.</returns>
    internal static bool HasExtensionSchema(ReadOnlySpan<byte> packet) => packet.IndexOf(">http://pdfa.org/declarations/<"u8) >= 0;

    /// <summary>Finds where the list item that starts at a position ends, counting nested items.</summary>
    /// <param name="text">The packet text.</param>
    /// <param name="start">Where the item's opening tag starts.</param>
    /// <param name="rdf">The RDF prefix.</param>
    /// <returns>The position just after the item's closing tag, or -1 when the item is not balanced.</returns>
    private static int FindItemEnd(string text, int start, string rdf)
    {
        var open = $"<{rdf}:li";
        var close = $"</{rdf}:li>";
        var depth = 0;
        var position = start;
        while (position < text.Length)
        {
            var nextOpen = text.IndexOf(open, position, StringComparison.Ordinal);
            var nextClose = text.IndexOf(close, position, StringComparison.Ordinal);
            if (nextClose < 0)
            {
                return -1;
            }

            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                depth++;
                position = nextOpen + open.Length;
                continue;
            }

            depth--;
            position = nextClose + close.Length;
            if (depth == 0)
            {
                return position;
            }
        }

        return -1;
    }

    /// <summary>Determines whether text is an absolute address with no spaces or control characters.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsAbsoluteUri(string text) =>
        text.Length <= MaxFieldLength && !HasWhiteSpaceOrControl(text) && Uri.TryCreate(text, UriKind.Absolute, out _);

    /// <summary>Determines whether text names a report embedded in the file.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it is <c>#ef=</c> and a name.</returns>
    private static bool IsEmbeddedReport(string text) =>
        text.Length > EmbeddedReportFragment.Length && text.Length <= MaxFieldLength
        && text.StartsWith(EmbeddedReportFragment, StringComparison.Ordinal) && !HasControl(text);

    /// <summary>Determines whether text is an XMP date.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsXmpDate(string text) =>
        DateTimeOffset.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);

    /// <summary>Determines whether text has a white space or control character.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasWhiteSpaceOrControl(string text)
    {
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether text has a control character.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasControl(string text)
    {
        foreach (var character in text)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Refuses text the file cannot hold safely.</summary>
    /// <param name="value">The text, or <see langword="null"/>.</param>
    /// <param name="what">What the text is, for the message.</param>
    /// <exception cref="ArgumentException">The text is too long or has control characters.</exception>
    private static void CheckText(string? value, string what)
    {
        if (value is not null && (value.Length > MaxFieldLength || HasControl(value)))
        {
            throw new ArgumentException($"The {what} is too long or has characters a file cannot hold.", nameof(value));
        }
    }

    /// <summary>Writes one declaration as an RDF structure.</summary>
    /// <param name="prefix">The declarations prefix.</param>
    /// <param name="rdf">The RDF prefix.</param>
    /// <param name="declaration">The declaration.</param>
    /// <returns>The XML.</returns>
    private static string BuildItem(string prefix, string rdf, PdfDeclaration declaration)
    {
        var xml = new StringBuilder();
        var profile = SecurityElement.Escape(declaration.ConformsTo);
        _ = xml.Append(CultureInfo.InvariantCulture, $"<{rdf}:li {rdf}:parseType=\"Resource\"><{prefix}:conformsTo>{profile}</{prefix}:conformsTo>");
        if (declaration.ClaimData.Length != 0)
        {
            _ = xml.Append(CultureInfo.InvariantCulture, $"<{prefix}:claimData><{rdf}:Bag>");
            foreach (var claim in declaration.ClaimData)
            {
                AppendClaim(xml, prefix, rdf, claim);
            }

            _ = xml.Append(CultureInfo.InvariantCulture, $"</{rdf}:Bag></{prefix}:claimData>");
        }

        return xml.Append(CultureInfo.InvariantCulture, $"</{rdf}:li>").ToString();
    }

    /// <summary>Writes one claim's fields.</summary>
    /// <param name="xml">The output.</param>
    /// <param name="prefix">The declarations prefix.</param>
    /// <param name="rdf">The RDF prefix.</param>
    /// <param name="claim">The claim.</param>
    private static void AppendClaim(StringBuilder xml, string prefix, string rdf, PdfDeclarationClaim claim)
    {
        _ = xml.Append(CultureInfo.InvariantCulture, $"<{rdf}:li {rdf}:parseType=\"Resource\">");
        AppendField(xml, prefix, "claimBy", claim.ClaimBy);
        AppendField(xml, prefix, "claimDate", claim.ClaimDate);
        AppendField(xml, prefix, "claimCredentials", claim.ClaimCredentials);
        AppendField(xml, prefix, "claimReport", claim.ClaimReport);
        _ = xml.Append(CultureInfo.InvariantCulture, $"</{rdf}:li>");
    }

    /// <summary>Writes a field that is present.</summary>
    /// <param name="xml">The output.</param>
    /// <param name="prefix">The declarations prefix.</param>
    /// <param name="name">The field name.</param>
    /// <param name="value">The value, or <see langword="null"/> to write nothing.</param>
    private static void AppendField(StringBuilder xml, string prefix, string name, string? value)
    {
        if (value is not null)
        {
            _ = xml.Append(CultureInfo.InvariantCulture, $"<{prefix}:{name}>{SecurityElement.Escape(value)}</{prefix}:{name}>");
        }
    }

    /// <summary>Adds the declarations property in a new description at the end of the RDF element.</summary>
    /// <param name="text">The packet text.</param>
    /// <param name="prefix">The declarations prefix.</param>
    /// <param name="rdf">The RDF prefix.</param>
    /// <param name="item">The declaration's XML.</param>
    /// <returns>The new text, or <see langword="null"/> when the packet has no RDF element to add to.</returns>
    private static string? AddProperty(string text, string prefix, string rdf, string item)
    {
        var end = text.LastIndexOf($"</{rdf}:RDF>", StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        var opening = $"<{rdf}:Description {rdf}:about=\"\" xmlns:{prefix}=\"http://pdfa.org/declarations/\"><{prefix}:declarations><{rdf}:Bag>";
        return text.Insert(end, $"{opening}{item}</{rdf}:Bag></{prefix}:declarations></{rdf}:Description>");
    }

    /// <summary>Adds a declaration to the bag of an existing declarations property.</summary>
    /// <param name="text">The packet text.</param>
    /// <param name="opening">Where the property's opening tag starts.</param>
    /// <param name="prefix">The declarations prefix.</param>
    /// <param name="rdf">The RDF prefix.</param>
    /// <param name="item">The declaration's XML.</param>
    /// <returns>The new text, or <see langword="null"/> when the property is not a bag in element form.</returns>
    private static string? AddToBag(string text, int opening, string prefix, string rdf, string item)
    {
        var closing = text.IndexOf($"</{prefix}:declarations>", opening, StringComparison.Ordinal);
        if (closing < 0)
        {
            return null;
        }

        var bagEnd = text.LastIndexOf($"</{rdf}:Bag>", closing, StringComparison.Ordinal);
        return bagEnd < opening ? null : text.Insert(bagEnd, item);
    }
}
