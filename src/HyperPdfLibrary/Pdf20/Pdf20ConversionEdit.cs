// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>Edits a dictionary copy-on-write for the dictionary pass: the original is never changed, and a copy is made on the first edit.</summary>
internal static class Pdf20ConversionEdit
{
    /// <summary>Removes an entry.</summary>
    /// <param name="original">The dictionary the pass is fixing.</param>
    /// <param name="copy">The copy, made on the first edit.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the entry was there.</returns>
    internal static bool Remove(PdfDictionary original, ref PdfDictionary? copy, PdfName key)
    {
        if (!(copy ?? original).ContainsKey(key))
        {
            return false;
        }

        copy ??= original.Clone();
        _ = copy.Remove(key);
        return true;
    }

    /// <summary>Sets an entry.</summary>
    /// <param name="original">The dictionary the pass is fixing.</param>
    /// <param name="copy">The copy, made on the first edit.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    internal static void Set(PdfDictionary original, ref PdfDictionary? copy, PdfName key, PdfValue value)
    {
        copy ??= original.Clone();
        copy.Set(key, value);
    }
}
