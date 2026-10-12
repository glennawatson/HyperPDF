// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks a stream dictionary's keys: no external file (ISO 19005-2, 6.1.7.1) and only the standard filters
/// (ISO 19005-2, 6.1.7.2; ISO 19005-1, 6.1.10). It picks the filters veraPDF checks, which, when /DecodeParms is not a
/// single dictionary per filter, are fewer than the stream lists.
/// </summary>
internal static class PdfAStreamDictionaryRules
{
    /// <summary>Records the rules a stream's dictionary breaks.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="findings">Receives the broken rules.</param>
    internal static void Check(PdfStream stream, PdfAObjectFindings findings)
    {
        var dictionary = stream.Dictionary;
        var external = !dictionary.Get(KnownName.F).IsNull || HasFilterNames(dictionary.Get(KnownName.FFilter)) || !dictionary.Get(KnownName.FDecodeParms).IsNull;
        findings.Require(!external, PdfARule.StreamExternalFile);

        var filters = dictionary.Get(KnownName.Filter);
        var parameters = dictionary.Get(KnownName.DecodeParms);
        if (filters.AsArray() is { } list)
        {
            CheckFilterList(list, parameters, findings);
        }
        else if (filters.TryGetName(out var name))
        {
            CheckFilter(name, parameters.AsArray() is { } array ? array.Get(0) : parameters, findings);
        }
    }

    /// <summary>Checks one filter: never LZW, and in PDF/A-2 and later only the standard filters.</summary>
    /// <param name="name">The filter name.</param>
    /// <param name="parameters">The filter's parameters.</param>
    /// <param name="findings">Receives the broken rules.</param>
    internal static void CheckFilter(PdfName name, PdfValue parameters, PdfAObjectFindings findings)
    {
        var known = name.ToKnownName();
        findings.Require(known != KnownName.LZWDecode, PdfARule.FilterLzwForbidden);
        var allowed = known switch
        {
            KnownName.ASCIIHexDecode or KnownName.ASCII85Decode or KnownName.FlateDecode or KnownName.RunLengthDecode
                or KnownName.CCITTFaxDecode or KnownName.JBIG2Decode or KnownName.DCTDecode or KnownName.JPXDecode => true,
            KnownName.Crypt => IsIdentityCrypt(parameters),
            _ => false,
        };
        findings.Require(allowed, PdfARule.FilterAllowed);
    }

    /// <summary>Checks the filters of a list as veraPDF does: all of them without /DecodeParms, otherwise only the first.</summary>
    /// <param name="list">The filters.</param>
    /// <param name="parameters">The /DecodeParms value.</param>
    /// <param name="findings">Receives the broken rules.</param>
    private static void CheckFilterList(PdfArray list, PdfValue parameters, PdfAObjectFindings findings)
    {
        if (list.Count == 1)
        {
            if (list.Get(0).TryGetName(out var single))
            {
                CheckFilter(single, parameters.AsArray() is { } array ? array.Get(0) : parameters, findings);
            }

            return;
        }

        if (parameters.IsNull)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list.Get(i).TryGetName(out var name))
                {
                    CheckFilter(name, default, findings);
                }
            }

            return;
        }

        // veraPDF replaces its parameters with the first entry while it walks the list, so only the first filter is checked.
        if (parameters.AsArray() is { Count: > 0 } entries && list.Get(0).TryGetName(out var first))
        {
            CheckFilter(first, entries.Get(0), findings);
        }
    }

    /// <summary>Determines whether a Crypt filter's parameters name the Identity filter, as a missing dictionary does.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    private static bool IsIdentityCrypt(PdfValue parameters)
    {
        if (parameters.AsDictionary() is not { } dictionary)
        {
            return true;
        }

        var owner = dictionary.Owner;
        return owner is not null && dictionary.Get(KnownName.Name).TryGetName(out var name) && owner.Names.NameEquals(name, "Identity"u8);
    }

    /// <summary>Determines whether /FFilter holds a name or names, which veraPDF reads as present.</summary>
    /// <param name="value">The /FFilter value.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasFilterNames(PdfValue value) => value.Kind == PdfKind.Name || value.AsArray() is { Count: > 0 };
}
