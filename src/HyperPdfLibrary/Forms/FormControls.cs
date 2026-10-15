// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>The widgets of one field, in kid order, in a pooled array. Dispose gives the array back.</summary>
[DebuggerDisplay("FormControls: {Count} controls")]
internal ref struct FormControls
{
    /// <summary>The controls; a pooled array.</summary>
    private FormControl[]? _items;

    /// <summary>Gets the number of controls.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets a control.</summary>
    /// <param name="index">The control's place.</param>
    internal readonly FormControl this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items![index];
    }

    /// <summary>Lists the widgets that belong to a field: its widget kids, or the field itself when it is a widget.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="fieldId">The field's object id.</param>
    /// <returns>The controls in kid order; dispose them when done.</returns>
    internal static FormControls Read(PdfDictionary field, PdfObjectId fieldId)
    {
        var controls = default(FormControls);
        try
        {
            if (field.GetArray(KnownName.Kids) is not { } kids)
            {
                if (field.IsName(KnownName.Subtype, KnownName.Widget))
                {
                    controls.Add(new(field, fieldId));
                }

                return controls;
            }

            for (var i = 0; i < kids.Count; i++)
            {
                if (kids.GetDictionary(i) is { } kid && kid.IsName(KnownName.Subtype, KnownName.Widget))
                {
                    controls.Add(new(kid, kids.GetRaw(i).AsReference()));
                }
            }

            return controls;
        }
        catch
        {
            controls.Dispose();
            throw;
        }
    }

    /// <summary>Gets a borrowed view of the controls.</summary>
    /// <returns>The controls, valid until disposal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ReadOnlySpan<FormControl> AsSpan() => _items.AsSpan(0, Count);

    /// <summary>Gives the array back.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Dispose()
    {
        PooledArrays.Return(ref _items, Count, clear: true);
        Count = 0;
    }

    /// <summary>Adds a control.</summary>
    /// <param name="control">The control.</param>
    private void Add(in FormControl control)
    {
        PooledArrays.EnsureRoom(ref _items, Count, clear: true)[Count] = control;
        Count++;
    }
}
