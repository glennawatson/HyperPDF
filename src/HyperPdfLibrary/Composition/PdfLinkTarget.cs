// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>Where a link goes: to a web address or file, to a bookmark in the document, or to a page.</summary>
[DebuggerDisplay("PdfLinkTarget: {Kind} {Value} {Page}")]
public sealed record PdfLinkTarget
{
    /// <summary>Initializes a new instance of the <see cref="PdfLinkTarget"/> class.</summary>
    /// <param name="kind">What the link goes to.</param>
    /// <param name="value">The address or bookmark name.</param>
    /// <param name="page">The page number, from 1.</param>
    private PdfLinkTarget(PdfLinkKind kind, string value, int page)
    {
        Kind = kind;
        Value = value;
        Page = page;
    }

    /// <summary>Gets what the link goes to.</summary>
    public PdfLinkKind Kind { get; }

    /// <summary>Gets the address or the bookmark name; empty for a page link.</summary>
    public string Value { get; }

    /// <summary>Gets the page number counted from 1 for a page link; zero otherwise.</summary>
    public int Page { get; }

    /// <summary>Makes a link to an address such as a web page.</summary>
    /// <param name="uri">The address.</param>
    /// <returns>The target.</returns>
    /// <exception cref="ArgumentException">The address is empty.</exception>
    public static PdfLinkTarget ToUri(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        return new(PdfLinkKind.Uri, uri, 0);
    }

    /// <summary>Makes a link to a <see cref="PdfBookmark"/> in the document.</summary>
    /// <param name="name">The bookmark's name.</param>
    /// <returns>The target.</returns>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    public static PdfLinkTarget ToBookmark(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return new(PdfLinkKind.Bookmark, name, 0);
    }

    /// <summary>Makes a link to the top of a page.</summary>
    /// <param name="page">The page's number in the document, counted from 1.</param>
    /// <returns>The target.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page number is less than 1.</exception>
    public static PdfLinkTarget ToPage(int page)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        return new(PdfLinkKind.Page, string.Empty, page);
    }
}
