// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace HyperPdf.Core.Tests.Speech.Listening;

/// <summary>Source-generated JSON for the listening corpus.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ListeningCorpus))]
internal sealed partial class ListeningJsonContext : JsonSerializerContext;
