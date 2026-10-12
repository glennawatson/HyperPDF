// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Http.Speech;

/// <summary>The person's own Azure Speech resource.</summary>
/// <param name="Key">The subscription key.</param>
/// <param name="Region">The region, for example <c>uksouth</c>.</param>
[DebuggerDisplay("AzureSpeechSettings: {Region}")]
public sealed record AzureSpeechSettings(string Key, string Region)
{
    /// <summary>Gets a value indicating whether a key and a valid region are set.</summary>
    public bool IsComplete => !string.IsNullOrWhiteSpace(Key) && AzureSpeechEngine.IsValidRegion(Region);

    /// <summary>Keeps the key out of logs and debugger views.</summary>
    /// <returns>The region only.</returns>
    public override string ToString() => $"Azure Speech ({Region})";
}
