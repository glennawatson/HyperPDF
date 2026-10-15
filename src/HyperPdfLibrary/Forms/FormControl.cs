// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>One widget of a field: its dictionary and, when it is an indirect object, its id.</summary>
/// <param name="Dictionary">The widget dictionary.</param>
/// <param name="Id">The object id; not valid for a widget written directly inside another object.</param>
[DebuggerDisplay("FormControl: {Id}")]
internal readonly record struct FormControl(PdfDictionary Dictionary, PdfObjectId Id)
{
    /// <summary>Determines whether the widget is switched on.</summary>
    /// <param name="names">The document's names.</param>
    /// <returns><see langword="true"/> when the appearance state is the on-state.</returns>
    internal bool IsChecked(PdfNameTable names)
    {
        var on = FieldAttributes.GetOnStateName(Dictionary, names);
        if (on.IsNone)
        {
            return false;
        }

        var state = Dictionary.Get(KnownName.AS);
        return state.Kind == PdfKind.Name
            ? state.AsName().Equals(on)
            : state.Kind == PdfKind.String && string.Equals(FieldAttributes.ReadState(state, names), names.GetString(on), StringComparison.Ordinal);
    }
}
