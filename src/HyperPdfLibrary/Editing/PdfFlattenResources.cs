// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// A copy of a page's resources that the flattened drawings are added to: their forms and optional content groups, each
/// under a name the page does not use. The page's own resources, which other pages may share, are not changed.
/// </summary>
[DebuggerDisplay("PdfFlattenResources: {_forms.Count} forms")]
internal sealed class PdfFlattenResources
{
    /// <summary>The prefix of the names given to forms.</summary>
    private const string FormPrefix = "HpFlat";

    /// <summary>The prefix of the names given to graphics states.</summary>
    private const string StatePrefix = "HpFlatGs";

    /// <summary>The prefix of the names given to optional content groups.</summary>
    private const string LayerPrefix = "HpFlatOc";

    /// <summary>The document's objects.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The copy of the page's resources.</summary>
    private readonly PdfDictionary _resources;

    /// <summary>The copy of the /XObject dictionary.</summary>
    private readonly PdfDictionary _xobjects;

    /// <summary>The names given to forms, by object number.</summary>
    private readonly Dictionary<int, PdfName> _forms = [];

    /// <summary>The names given to optional content, by object number.</summary>
    private readonly Dictionary<int, PdfName> _layers = [];

    /// <summary>The names given to blend mode graphics states, by blend mode.</summary>
    private readonly Dictionary<PdfName, PdfName> _blends = [];

    /// <summary>The copy of the /Properties dictionary, made on first use.</summary>
    private PdfDictionary? _properties;

    /// <summary>The copy of the /ExtGState dictionary, made on first use.</summary>
    private PdfDictionary? _states;

    /// <summary>Initializes a new instance of the <see cref="PdfFlattenResources"/> class.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="resources">The page's resources, own or inherited, or <see langword="null"/>.</param>
    internal PdfFlattenResources(PdfObjectStore store, PdfDictionary? resources)
    {
        _store = store;
        _resources = resources?.Clone() ?? new PdfDictionary(store);
        _xobjects = _resources.GetDictionary(KnownName.XObject)?.Clone() ?? new PdfDictionary(store);
    }

    /// <summary>Gets the name a form is drawn by, adding it on first use.</summary>
    /// <param name="form">A reference to the form.</param>
    /// <returns>The name.</returns>
    internal PdfName UseForm(PdfValue form)
    {
        var number = form.AsReference().Number;
        if (_forms.TryGetValue(number, out var existing))
        {
            return existing;
        }

        var name = PdfPageStamping.UniqueName(_xobjects, FormPrefix, _store.Names);
        _xobjects.Set(name, form);
        _forms[number] = name;
        return name;
    }

    /// <summary>Gets the name an optional content group or membership dictionary is marked by, adding it on first use.</summary>
    /// <param name="layer">The value of the annotation's /OC.</param>
    /// <returns>The name.</returns>
    internal PdfName UseLayer(PdfValue layer)
    {
        if (layer.IsReference && _layers.TryGetValue(layer.AsReference().Number, out var existing))
        {
            return existing;
        }

        _properties ??= _resources.GetDictionary(KnownName.Properties)?.Clone() ?? new PdfDictionary(_store);
        var name = PdfPageStamping.UniqueName(_properties, LayerPrefix, _store.Names);
        _properties.Set(name, layer);
        if (layer.IsReference)
        {
            _layers[layer.AsReference().Number] = name;
        }

        return name;
    }

    /// <summary>Gets the name of a graphics state that sets a blend mode, adding it on first use.</summary>
    /// <param name="blend">The blend mode.</param>
    /// <returns>The name.</returns>
    internal PdfName UseBlend(PdfName blend)
    {
        if (_blends.TryGetValue(blend, out var existing))
        {
            return existing;
        }

        _states ??= _resources.GetDictionary(KnownName.ExtGState)?.Clone() ?? new PdfDictionary(_store);
        var state = new PdfDictionary(_store);
        state.Set(KnownName.Type, PdfValue.FromName(KnownName.ExtGState));
        state.Set(KnownName.BM, PdfValue.FromName(blend));
        var name = PdfPageStamping.UniqueName(_states, StatePrefix, _store.Names);
        _states.Set(name, PdfValue.FromDictionary(state));
        _blends[blend] = name;
        return name;
    }

    /// <summary>Gets the resources with everything that was added.</summary>
    /// <returns>The resources to store in the page.</returns>
    internal PdfDictionary Finish()
    {
        _resources.Set(KnownName.XObject, PdfValue.FromDictionary(_xobjects));
        if (_properties is not null)
        {
            _resources.Set(KnownName.Properties, PdfValue.FromDictionary(_properties));
        }

        if (_states is not null)
        {
            _resources.Set(KnownName.ExtGState, PdfValue.FromDictionary(_states));
        }

        return _resources;
    }
}
