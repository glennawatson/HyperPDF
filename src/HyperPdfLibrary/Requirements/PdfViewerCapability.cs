// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Requirements;

/// <summary>What a viewer provides for one requirement type.</summary>
/// <param name="Type">The requirement type name from ISO 32000-2 Table 275.</param>
/// <param name="Title">The feature in plain words, for messages to the user.</param>
/// <param name="Support">How much of the feature the viewer provides.</param>
/// <param name="SupportedVersion">The newest version of the feature the viewer provides, such as <c>2.0</c>; empty when it provides none.</param>
/// <param name="Note">What is missing or limited, in plain words; empty when nothing is.</param>
[DebuggerDisplay("PdfViewerCapability: {Type} {Support} {SupportedVersion}")]
public sealed record PdfViewerCapability(string Type, string Title, PdfRequirementSupport Support, string SupportedVersion, string Note);
