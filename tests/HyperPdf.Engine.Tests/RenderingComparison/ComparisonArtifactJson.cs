// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Serializes comparison evidence without runtime reflection.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ComparisonArtifact))]
[JsonSerializable(typeof(FractionalGroupArtifact))]
internal sealed partial class ComparisonArtifactJson : JsonSerializerContext;
