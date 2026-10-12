// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Forms;

/// <summary>Sets the tint drawn over fillable form fields when a page renders. Both engines draw it the same way.</summary>
public interface IFormHighlight : IDocumentFeature
{
    /// <summary>Gets or sets the tint. Pages rendered after the change use it.</summary>
    FormHighlight Highlight { get; set; }
}
