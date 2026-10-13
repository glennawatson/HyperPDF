// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>Reads the items of a /W or /W2 array one at a time with a three-state grammar: first CID, then an array or a last CID, then the values (ISO 32000-2, 9.7.4.3).</summary>
[DebuggerDisplay("CidMetricReader: state {_state}")]
internal sealed class CidMetricReader
{
    /// <summary>The state waiting for a first CID.</summary>
    private const int ExpectFirst = 0;

    /// <summary>The state after a first CID, waiting for an array or a last CID.</summary>
    private const int ExpectArrayOrLast = 1;

    /// <summary>The state collecting the numbers of a run.</summary>
    private const int ExpectValues = 2;

    /// <summary>The second value of a vertical entry.</summary>
    private const int OriginXIndex = 1;

    /// <summary>The third value of a vertical entry.</summary>
    private const int OriginYIndex = 2;

    /// <summary>The entries read.</summary>
    private readonly List<CidMetric> _entries;

    /// <summary>The numbers per CID.</summary>
    private readonly int _valueCount;

    /// <summary>The numbers of the run being read.</summary>
    private readonly float[] _values;

    /// <summary>The state.</summary>
    private int _state;

    /// <summary>The first CID of the current run.</summary>
    private long _first;

    /// <summary>The last CID of the current run.</summary>
    private long _last;

    /// <summary>The numbers collected so far.</summary>
    private int _collected;

    /// <summary>Initializes a new instance of the <see cref="CidMetricReader"/> class.</summary>
    /// <param name="entries">Receives the entries.</param>
    /// <param name="valueCount">The numbers per CID: 1 for /W, 3 for /W2.</param>
    internal CidMetricReader(List<CidMetric> entries, int valueCount)
    {
        _entries = entries;
        _valueCount = valueCount;
        _values = new float[valueCount];
    }

    /// <summary>Gets a value indicating whether the last entry is unfinished, so the array ended too soon.</summary>
    internal bool IsMidEntry => _state != ExpectFirst;

    /// <summary>Reads one array item.</summary>
    /// <param name="item">The item.</param>
    /// <returns><see langword="false"/> when the item is malformed and was skipped.</returns>
    internal bool Add(PdfValue item)
    {
        if (item.AsArray() is { } run)
        {
            if (_state != ExpectArrayOrLast)
            {
                return false;
            }

            AddRun(run);
            _state = ExpectFirst;
            return true;
        }

        if (!item.IsNumber)
        {
            return true;
        }

        AddNumber(item);
        return true;
    }

    /// <summary>Reads a number in the current state.</summary>
    /// <param name="item">The number.</param>
    private void AddNumber(PdfValue item)
    {
        switch (_state)
        {
            case ExpectFirst:
            {
                _first = item.AsInteger();
                _state = ExpectArrayOrLast;
                break;
            }

            case ExpectArrayOrLast:
            {
                _last = item.AsInteger();
                _state = ExpectValues;
                _collected = 0;
                break;
            }

            default:
            {
                _values[_collected] = item.AsSingle();
                _collected++;
                if (_collected == _valueCount)
                {
                    _entries.Add(Make(_first, _last));
                    _state = ExpectFirst;
                }

                break;
            }
        }
    }

    /// <summary>Reads <c>c [values...]</c>: consecutive CIDs from the first, each with its own values.</summary>
    /// <param name="run">The array of values.</param>
    private void AddRun(PdfArray run)
    {
        var cid = _first;
        for (var j = 0; j + _valueCount <= run.Count; j += _valueCount)
        {
            for (var k = 0; k < _valueCount; k++)
            {
                _values[k] = run.GetSingle(j + k);
            }

            _entries.Add(Make(cid, cid));
            cid++;
        }
    }

    /// <summary>Makes an entry from the collected values.</summary>
    /// <param name="first">The first CID.</param>
    /// <param name="last">The last CID.</param>
    /// <returns>The entry.</returns>
    private CidMetric Make(long first, long last)
    {
        var x = _valueCount > OriginXIndex ? _values[OriginXIndex] : 0;
        var y = _valueCount > OriginYIndex ? _values[OriginYIndex] : 0;
        return new((int)Math.Clamp(first, int.MinValue, int.MaxValue), (int)Math.Clamp(last, int.MinValue, int.MaxValue), _values[0], x, y);
    }
}
