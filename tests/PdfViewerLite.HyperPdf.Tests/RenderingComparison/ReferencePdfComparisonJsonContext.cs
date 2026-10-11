// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace PdfViewerLite.HyperPdf.Tests.RenderingComparison;

/// <summary>Serializes reference PDF comparison evidence without reflection.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ReferencePdfComparisonArtifact))]
internal sealed partial class ReferencePdfComparisonJsonContext : JsonSerializerContext;
