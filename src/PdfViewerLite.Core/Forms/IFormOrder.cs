// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.Core.Forms;

/// <summary>Reads the orders a form sets for calculating fields and for moving between them with the keyboard.</summary>
public interface IFormOrder : IDocumentFeature
{
    /// <summary>Appends the names of the calculated fields in the order the form recalculates them.</summary>
    /// <param name="output">The list receiving the names.</param>
    void GetCalculationOrder(List<string> output);

    /// <summary>Appends the widget indexes of a page in the order the Tab key visits them, following the page's <c>/Tabs</c> entry.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the widget indexes.</param>
    void GetTabOrder(int pageIndex, List<int> output);
}
