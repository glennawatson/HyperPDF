// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// A GSUB or GPOS table (OpenType, "OpenType Layout common table formats"): its script, language system, feature and
/// lookup lists, read in place from the font bytes.
/// </summary>
[DebuggerDisplay("OtLayoutTable: {LookupCount} lookups")]
internal sealed class OtLayoutTable
{
    /// <summary>The offset of the script list offset in the header.</summary>
    private const int ScriptListField = 4;

    /// <summary>The offset of the feature list offset in the header.</summary>
    private const int FeatureListField = 6;

    /// <summary>The offset of the lookup list offset in the header.</summary>
    private const int LookupListField = 8;

    /// <summary>The bytes of the header's fixed part.</summary>
    private const int HeaderBytes = 10;

    /// <summary>The bytes of a tag and offset record.</summary>
    private const int TagRecordBytes = 6;

    /// <summary>The offset of the first record after a count.</summary>
    private const int RecordsOffset = 2;

    /// <summary>The offset of the language system count in a Script table.</summary>
    private const int LangSysCountOffset = 2;

    /// <summary>The offset of the first language system record in a Script table.</summary>
    private const int LangSysRecordsOffset = 4;

    /// <summary>The offset of the required feature index in a LangSys table.</summary>
    private const int RequiredFeatureOffset = 2;

    /// <summary>The offset of the feature index count in a LangSys table.</summary>
    private const int FeatureCountOffset = 4;

    /// <summary>The offset of the first feature index in a LangSys table.</summary>
    private const int FeatureIndicesOffset = 6;

    /// <summary>The required feature index that means there is none.</summary>
    private const int NoRequiredFeature = 0xFFFF;

    /// <summary>The offset of the lookup count in a Feature table.</summary>
    private const int FeatureLookupCountOffset = 2;

    /// <summary>The offset of the first lookup index in a Feature table.</summary>
    private const int FeatureLookupsOffset = 4;

    /// <summary>The table's bytes.</summary>
    private readonly ReadOnlyMemory<byte> _data;

    /// <summary>The offset of the script list, or zero.</summary>
    private readonly int _scripts;

    /// <summary>The offset of the feature list, or zero.</summary>
    private readonly int _features;

    /// <summary>The offset of the lookup list, or zero.</summary>
    private readonly int _lookups;

    /// <summary>Initializes a new instance of the <see cref="OtLayoutTable"/> class.</summary>
    /// <param name="data">The table's bytes, or empty when the font has none.</param>
    internal OtLayoutTable(ReadOnlyMemory<byte> data)
    {
        _data = data;
        var table = data.Span;
        if (table.Length < HeaderBytes)
        {
            return;
        }

        _scripts = FontBytes.U16(table, ScriptListField);
        _features = FontBytes.U16(table, FeatureListField);
        _lookups = FontBytes.U16(table, LookupListField);
    }

    /// <summary>Gets the table's bytes.</summary>
    internal ReadOnlySpan<byte> Span => _data.Span;

    /// <summary>Gets a value indicating whether the table has lookups to apply.</summary>
    internal bool IsPresent => _lookups != 0 && LookupCount > 0;

    /// <summary>Gets the number of lookups.</summary>
    internal int LookupCount => _lookups == 0 ? 0 : FontBytes.U16(_data.Span, _lookups);

    /// <summary>Gets the offset of a Lookup table.</summary>
    /// <param name="index">The lookup's index in the lookup list.</param>
    /// <returns>The offset from the start of the table, or -1 when the index is out of range.</returns>
    internal int LookupOffset(int index)
    {
        if ((uint)index >= (uint)LookupCount)
        {
            return -1;
        }

        var offset = FontBytes.U16(_data.Span, _lookups + RecordsOffset + (index * FontBytes.U16Size));
        return offset == 0 ? -1 : _lookups + offset;
    }

    /// <summary>Finds a script.</summary>
    /// <param name="tag">The script tag.</param>
    /// <param name="offset">Receives the offset of the Script table.</param>
    /// <returns><see langword="true"/> when the table has the script.</returns>
    internal bool TryFindScript(uint tag, out int offset)
    {
        var table = _data.Span;
        var count = _scripts == 0 ? 0 : FontBytes.U16(table, _scripts);
        for (var i = 0; i < count; i++)
        {
            var record = _scripts + RecordsOffset + (i * TagRecordBytes);
            if (FontBytes.U32(table, record) != tag)
            {
                continue;
            }

            offset = _scripts + FontBytes.U16(table, record + FontBytes.U32Size);
            return true;
        }

        offset = 0;
        return false;
    }

    /// <summary>Finds a language system of a script, falling back to the script's default.</summary>
    /// <param name="script">The offset of the Script table.</param>
    /// <param name="language">The language tag, or zero for the default.</param>
    /// <returns>The offset of the LangSys table, or -1 when the script has none.</returns>
    internal int FindLangSys(int script, uint language)
    {
        var table = _data.Span;
        if (language != 0)
        {
            var count = FontBytes.U16(table, script + LangSysCountOffset);
            for (var i = 0; i < count; i++)
            {
                var record = script + LangSysRecordsOffset + (i * TagRecordBytes);
                if (FontBytes.U32(table, record) == language)
                {
                    return script + FontBytes.U16(table, record + FontBytes.U32Size);
                }
            }
        }

        var fallback = FontBytes.U16(table, script);
        return fallback == 0 ? -1 : script + fallback;
    }

    /// <summary>Gets the lookups of the feature a language system requires in every case.</summary>
    /// <param name="langSys">The offset of the LangSys table.</param>
    /// <returns>The offset of the required Feature table, or -1 when there is none.</returns>
    internal int RequiredFeature(int langSys)
    {
        var index = FontBytes.U16(_data.Span, langSys + RequiredFeatureOffset);
        return index == NoRequiredFeature ? -1 : FeatureOffset(index);
    }

    /// <summary>Lists the Feature tables of a language system that have a tag.</summary>
    /// <param name="langSys">The offset of the LangSys table.</param>
    /// <param name="tag">The feature tag.</param>
    /// <param name="found">Receives the offsets of the Feature tables; the list is not cleared.</param>
    internal void FindFeatures(int langSys, uint tag, List<int> found)
    {
        var table = _data.Span;
        var count = FontBytes.U16(table, langSys + FeatureCountOffset);
        for (var i = 0; i < count; i++)
        {
            var index = FontBytes.U16(table, langSys + FeatureIndicesOffset + (i * FontBytes.U16Size));
            if (FeatureTag(index) == tag)
            {
                found.Add(FeatureOffset(index));
            }
        }
    }

    /// <summary>Lists the lookups a Feature table refers to.</summary>
    /// <param name="feature">The offset of the Feature table.</param>
    /// <param name="found">Receives the lookup indexes; the list is not cleared.</param>
    internal void FeatureLookups(int feature, List<int> found)
    {
        var table = _data.Span;
        var count = FontBytes.U16(table, feature + FeatureLookupCountOffset);
        for (var i = 0; i < count; i++)
        {
            found.Add(FontBytes.U16(table, feature + FeatureLookupsOffset + (i * FontBytes.U16Size)));
        }
    }

    /// <summary>Gets the tag of a feature.</summary>
    /// <param name="index">The feature's index in the feature list.</param>
    /// <returns>The tag; zero when the index is out of range.</returns>
    private uint FeatureTag(int index)
    {
        var table = _data.Span;
        return _features == 0 || (uint)index >= (uint)FontBytes.U16(table, _features)
            ? 0
            : FontBytes.U32(table, _features + RecordsOffset + (index * TagRecordBytes));
    }

    /// <summary>Gets the offset of a Feature table.</summary>
    /// <param name="index">The feature's index in the feature list.</param>
    /// <returns>The offset from the start of the table; -1 when the index is out of range.</returns>
    private int FeatureOffset(int index)
    {
        var table = _data.Span;
        return _features == 0 || (uint)index >= (uint)FontBytes.U16(table, _features)
            ? -1
            : _features + FontBytes.U16(table, _features + RecordsOffset + (index * TagRecordBytes) + FontBytes.U32Size);
    }
}
