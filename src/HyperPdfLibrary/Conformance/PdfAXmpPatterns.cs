// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace HyperPdfLibrary.Conformance;

/// <summary>The patterns of the XMP simple value types (XMP Specification Part 1, 8.2.1) that PDF/A checks.</summary>
internal static partial class PdfAXmpPatterns
{
    /// <summary>Matches a Boolean: <c>True</c> or <c>False</c>.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\A(?:True|False)\z", RegexOptions.CultureInvariant)]
    internal static partial Regex Boolean();

    /// <summary>Matches an Integer.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\A[+-]?[0-9]+\z", RegexOptions.CultureInvariant)]
    internal static partial Regex Integer();

    /// <summary>Matches a Real.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\A(?:[+-]?[0-9]+\.?[0-9]*|[+-]?[0-9]*\.?[0-9]+)\z", RegexOptions.CultureInvariant)]
    internal static partial Regex Real();

    /// <summary>Matches a MIMEType.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\A[-a-zA-Z0-9_+.]+/[-a-zA-Z0-9_+.]+\z", RegexOptions.CultureInvariant)]
    internal static partial Regex MimeType();

    /// <summary>Matches a GPSCoordinate as the 2004 XMP specification writes one, for PDF/A-1.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\A[0-9]{2},[0-9]{2}[,.][0-9]{2}[NSEW]\z", RegexOptions.CultureInvariant)]
    internal static partial Regex GpsCoordinate2004();

    /// <summary>Matches a GPSCoordinate as the 2005 XMP specification writes one, for PDF/A-2 and PDF/A-3.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\A[0-9]{1,3},[0-9]{1,2}(?:,[0-9]{1,2}|\.[0-9]+)[NSEW]\z", RegexOptions.CultureInvariant)]
    internal static partial Regex GpsCoordinate2005();

    /// <summary>Matches the "Choice of" wording that extension schemas put before a value type.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("(?:open |closed )?(?:choice |choice$)(?:of )?", RegexOptions.CultureInvariant)]
    internal static partial Regex ChoiceWording();

    /// <summary>Matches a <c>bytes</c> attribute in a packet header.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("bytes[ \t\n\v\f\r]*=[ \t\n\v\f\r]*(?:'[^']*'|\"[^\"]*\")", RegexOptions.CultureInvariant)]
    internal static partial Regex BytesAttribute();

    /// <summary>Matches an <c>encoding</c> attribute in a packet header.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("encoding[ \t\n\v\f\r]*=[ \t\n\v\f\r]*(?:'[^']*'|\"[^\"]*\")", RegexOptions.CultureInvariant)]
    internal static partial Regex EncodingAttribute();
}
