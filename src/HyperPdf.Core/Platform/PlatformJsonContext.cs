// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace HyperPdf.Core.Platform;

/// <summary>Source generated JSON metadata for the desktop integration's files, so no reflection is needed under Native AOT.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<RecentDocument>))]
internal sealed partial class PlatformJsonContext : JsonSerializerContext;
