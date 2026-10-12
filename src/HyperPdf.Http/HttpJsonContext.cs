// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;
using HyperPdf.Http.GitHub;

namespace HyperPdf.Http;

/// <summary>Source generated JSON metadata for HTTP payloads, keeping Refit reflection free under Native AOT.</summary>
[JsonSerializable(typeof(GitHubRelease))]
internal sealed partial class HttpJsonContext : JsonSerializerContext;
