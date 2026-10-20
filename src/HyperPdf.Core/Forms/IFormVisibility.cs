// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Forms;

/// <summary>Shows and hides form fields by name, as a form script's <c>display</c> setting asks.</summary>
public interface IFormVisibility : IDocumentFeature
{
    /// <summary>Hides or shows every widget of a field.</summary>
    /// <param name="fieldName">The field's full name.</param>
    /// <param name="hidden">Whether to hide the field.</param>
    /// <returns><see langword="true"/> when a widget changed.</returns>
    bool SetHidden(string fieldName, bool hidden);
}
