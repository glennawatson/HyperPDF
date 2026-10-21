// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Http.Signatures;

namespace HyperPdf.ViewModels.Signing;

/// <summary>Makes the client for the timestamp server address in Preferences.</summary>
internal static class TimestampServers
{
    /// <summary>Makes the timestamp client for a server address.</summary>
    /// <param name="address">The address; may be empty or not an address.</param>
    /// <returns>The client, or <see langword="null"/> when there is no usable address.</returns>
    internal static TimestampAuthorityClient? Create(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var server) ? new TimestampAuthorityClient(server) : null;
}
