// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Builders shared by the colour, function and image fix tests.</summary>
internal static class FixTestHelpers
{
    /// <summary>
    /// Opens the smallest document the object store can read, whose name table dictionaries can use. It has no
    /// cross-reference table, so the store repairs it.
    /// </summary>
    /// <returns>The store.</returns>
    internal static PdfObjectStore Store() =>
        StoreOpening.Open(
        "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\ntrailer\n<< /Root 1 0 R /Size 3 >>\n"u8.ToArray(),
        null);

    /// <summary>Creates a name value.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The value.</returns>
    internal static PdfValue Name(KnownName name) => PdfValue.FromName(name);

    /// <summary>Creates a number array value.</summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The value.</returns>
    internal static PdfValue Numbers(params float[] values) => PdfValue.FromArray(PdfArray.FromNumbers(null, values));

    /// <summary>Creates an array value.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The value.</returns>
    internal static PdfValue Array(params PdfValue[] items) => PdfValue.FromArray(new(null, items));

    /// <summary>Creates an array value owned by a document.</summary>
    /// <param name="owner">The document.</param>
    /// <param name="items">The items.</param>
    /// <returns>The value.</returns>
    internal static PdfValue Array(PdfObjectStore owner, params PdfValue[] items) => PdfValue.FromArray(new(owner, items));

    /// <summary>Creates a dictionary with one entry.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns>The dictionary.</returns>
    internal static PdfDictionary Dictionary(KnownName key, PdfValue value)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(key, value);
        return dictionary;
    }

    /// <summary>Gets the opaque BGRA bytes of a colour.</summary>
    /// <param name="red">The red byte.</param>
    /// <param name="green">The green byte.</param>
    /// <param name="blue">The blue byte.</param>
    /// <returns>The pixel bytes.</returns>
    internal static byte[] Bgra(byte red, byte green, byte blue) => [blue, green, red, byte.MaxValue];
}
