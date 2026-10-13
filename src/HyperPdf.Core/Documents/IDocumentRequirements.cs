// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Checks the requirements a document declares against what the viewer provides, without running any script.</summary>
public interface IDocumentRequirements : IDocumentFeature
{
    /// <summary>Checks the document's requirements.</summary>
    /// <returns>What the requirements mean; <see cref="RequirementReport.None"/> when the document has none or all are met.</returns>
    RequirementReport Check();
}
