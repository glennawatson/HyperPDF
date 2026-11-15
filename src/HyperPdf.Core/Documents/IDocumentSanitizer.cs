// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Takes hidden information out of the open document before it is shared.</summary>
public interface IDocumentSanitizer : IDocumentFeature
{
    /// <summary>Takes the chosen things out of the document as one undo step.</summary>
    /// <param name="choices">What to take out.</param>
    /// <returns>What was taken out.</returns>
    SanitizeSummary Sanitize(SanitizeChoices choices);
}
