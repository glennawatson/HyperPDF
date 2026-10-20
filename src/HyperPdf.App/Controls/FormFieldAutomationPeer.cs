// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Automation.Provider;
using HyperPdf.Core.Reading;

namespace HyperPdf.App.Controls;

/// <summary>
/// A form field of the shown page for assistive technology, with its value as a read-only value. The value is how a screen
/// reader reaches what is typed or chosen; a field is filled in with the form tools, not through assistive technology.
/// </summary>
[DebuggerDisplay("FormFieldAutomationPeer: {Item.Name}")]
internal sealed class FormFieldAutomationPeer : PageSemanticAutomationPeer, IValueProvider
{
    /// <summary>Initializes a new instance of the <see cref="FormFieldAutomationPeer"/> class.</summary>
    /// <param name="owner">The page canvas' peer.</param>
    /// <param name="page">The page the field is on.</param>
    /// <param name="index">The field's place among the page's items.</param>
    /// <param name="item">The field.</param>
    internal FormFieldAutomationPeer(PageCanvasAutomationPeer owner, int page, int index, PageSemanticItem item)
        : base(owner, page, index, item)
    {
    }

    /// <inheritdoc/>
    public bool IsReadOnly => true;

    /// <inheritdoc/>
    public string? Value => Item.Value ?? Item.State;

    /// <inheritdoc/>
    public void SetValue(string? value) => throw new InvalidOperationException("A form field is filled in with the form tools.");
}
