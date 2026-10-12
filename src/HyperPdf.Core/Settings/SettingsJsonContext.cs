// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace HyperPdf.Core.Settings;

/// <summary>Source generated JSON metadata for settings, keeping serialisation reflection free for Native AOT.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(SavedSignatureMarks))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
