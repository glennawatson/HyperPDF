#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace PdfViewerLite.Scripts;

/// <summary>Fetches explicitly selected, checksum-pinned PDF comparison fixtures.</summary>
internal static class FetchComparisonCorpus
{
    /// <summary>The minimum number of positional arguments.</summary>
    private const int MinimumArgumentCount = 3;

    /// <summary>The index of the first corpus ID argument.</summary>
    private const int FirstDocumentArgument = 2;

    /// <summary>The maximum number of HTTPS redirects accepted per entry.</summary>
    private const int MaximumRedirects = 5;

    /// <summary>The maximum download and manifest size.</summary>
    private const int MaximumDocumentBytes = 256 * 1024 * 1024;

    /// <summary>The maximum JSON nesting depth accepted in the manifest.</summary>
    private const int MaximumManifestDepth = 16;

    /// <summary>The maximum length of a safe corpus ID.</summary>
    private const int MaximumIdLength = 96;

    /// <summary>The number of hexadecimal characters used for one byte.</summary>
    private const int HexDigitsPerByte = 2;

    /// <summary>The streaming buffer size.</summary>
    private const int BufferSize = 65_536;

    /// <summary>The overall download timeout in minutes.</summary>
    private const int TimeoutMinutes = 10;

    /// <summary>The number of attempts for a transient corpus-host failure.</summary>
    private const int MaximumDownloadAttempts = 3;

    /// <summary>The most alternate HTTPS hosts a corpus entry may name.</summary>
    private const int MaximumMirrors = 2;

    /// <summary>The base backoff in milliseconds between transient failures.</summary>
    private const int RetryDelayMilliseconds = 500;

    /// <summary>The extension used for downloaded PDF documents.</summary>
    private const string DocumentExtension = ".pdf";

    /// <summary>The HTTP user agent sent to corpus hosts.</summary>
    private const string UserAgent = "PdfViewerLite comparison corpus verifier";

    /// <summary>Runs the fetcher with a manifest path, destination directory and one or more IDs.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>A task for the completed download operation.</returns>
    /// <exception cref="ArgumentException">The arguments are missing or contain an unsafe ID.</exception>
    /// <exception cref="InvalidDataException">The manifest or a downloaded entry does not match its expected format or digest.</exception>
    /// <exception cref="HttpRequestException">A corpus server returns an unsuccessful response or too many redirects.</exception>
    internal static async Task Main(string[] args)
    {
        if (args.Length < MinimumArgumentCount)
        {
            throw new ArgumentException("Provide a manifest path, destination directory and one or more corpus IDs.");
        }

        var manifestPath = Path.GetFullPath(args[0]);
        var destination = Path.GetFullPath(args[1]);
        var requestedIds = GetRequestedIds(args);
        _ = Directory.CreateDirectory(destination);

        var entries = await ReadManifestAsync(manifestPath, requestedIds);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(TimeoutMinutes));
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true };

        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        foreach (var requestedId in requestedIds)
        {
            await FetchOneAsync(client, entries[requestedId], destination, timeout.Token);
        }
    }

    /// <summary>Validates the requested IDs and returns them in command-line order.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The requested IDs.</returns>
    /// <exception cref="ArgumentException">An ID is unsafe or repeated.</exception>
    private static string[] GetRequestedIds(string[] args)
    {
        var ids = new string[args.Length - FirstDocumentArgument];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = FirstDocumentArgument; index < args.Length; index++)
        {
            var id = args[index];
            if (!IsSafeId(id))
            {
                throw new ArgumentException($"Unsafe corpus ID: {id}");
            }

            if (!seen.Add(id))
            {
                throw new ArgumentException($"Corpus ID was requested more than once: {id}");
            }

            ids[index - FirstDocumentArgument] = id;
        }

        return ids;
    }

    /// <summary>Reads the manifest and selects the requested entries.</summary>
    /// <param name="path">The manifest path.</param>
    /// <param name="requestedIds">The validated IDs to select.</param>
    /// <returns>The selected manifest entries.</returns>
    /// <exception cref="InvalidDataException">The manifest is malformed or exceeds the size limit.</exception>
    private static async Task<Dictionary<string, CorpusEntry>> ReadManifestAsync(string path, string[] requestedIds)
    {
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists || fileInfo.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException("The corpus manifest is missing or exceeds the size limit.");
        }

        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var document = await JsonDocument.ParseAsync(input, new JsonDocumentOptions { MaxDepth = MaximumManifestDepth });
        return ReadEntries(document.RootElement, requestedIds);
    }

    /// <summary>Selects and validates the requested rows from the manifest.</summary>
    /// <param name="root">The manifest root value.</param>
    /// <param name="requestedIds">The selected IDs.</param>
    /// <returns>The selected entries keyed by ID.</returns>
    /// <exception cref="InvalidDataException">The manifest has invalid entries or omits a requested ID.</exception>
    private static Dictionary<string, CorpusEntry> ReadEntries(JsonElement root, string[] requestedIds)
    {
        if (!root.TryGetProperty("documents", out var documents) || documents.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The corpus manifest must contain a documents array.");
        }

        var requested = new HashSet<string>(requestedIds, StringComparer.Ordinal);
        var entries = new Dictionary<string, CorpusEntry>(StringComparer.Ordinal);
        foreach (var document in documents.EnumerateArray())
        {
            var id = RequiredString(document, "id");
            if (!IsSafeId(id))
            {
                throw new InvalidDataException($"The corpus manifest contains an unsafe ID: {id}");
            }

            if (!requested.Contains(id))
            {
                continue;
            }

            if (!entries.TryAdd(id, ReadEntry(document, id)))
            {
                throw new InvalidDataException($"The corpus manifest contains duplicate ID {id}.");
            }
        }

        EnsureAllIdsExist(entries, requestedIds);
        return entries;
    }

    /// <summary>Reads and validates one selected manifest row.</summary>
    /// <param name="document">The row value.</param>
    /// <param name="id">The validated row ID.</param>
    /// <returns>The parsed row.</returns>
    /// <exception cref="InvalidDataException">The row contains an invalid URL, digest or byte count.</exception>
    private static CorpusEntry ReadEntry(JsonElement document, string id)
    {
        var uri = new Uri(RequiredString(document, "url"), UriKind.Absolute);
        EnsureHttps(uri);

        var hashText = RequiredString(document, "sha256");
        if (hashText.Length != SHA256.HashSizeInBytes * HexDigitsPerByte || !IsHex(hashText))
        {
            throw new InvalidDataException($"The corpus entry {id} has an invalid SHA-256 value.");
        }

        if (!document.TryGetProperty("bytes", out var byteValue) || !byteValue.TryGetInt64(out var expectedBytes) || expectedBytes <= 0 || expectedBytes > MaximumDocumentBytes)
        {
            throw new InvalidDataException($"The corpus entry {id} has an invalid byte count.");
        }

        return new(id, ReadUris(document, uri), Convert.FromHexString(hashText), expectedBytes);
    }

    /// <summary>Reads the primary HTTPS source and up to two explicit mirrors.</summary>
    /// <param name="document">The manifest row.</param>
    /// <param name="primary">The validated primary source.</param>
    /// <returns>The sources in attempt order.</returns>
    /// <exception cref="InvalidDataException">The mirror list is malformed or too long.</exception>
    private static Uri[] ReadUris(JsonElement document, Uri primary)
    {
        if (!document.TryGetProperty("mirrors", out var mirrors))
        {
            return [primary];
        }

        if (mirrors.ValueKind != JsonValueKind.Array || mirrors.GetArrayLength() > MaximumMirrors)
        {
            throw new InvalidDataException("A corpus entry has an invalid mirror list.");
        }

        var urls = new Uri[mirrors.GetArrayLength() + 1];
        urls[0] = primary;
        var index = 1;
        foreach (var mirror in mirrors.EnumerateArray())
        {
            if (mirror.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("A corpus mirror must be an HTTPS URL.");
            }

            var uri = new Uri(mirror.GetString()!, UriKind.Absolute);
            EnsureHttps(uri);
            urls[index] = uri;
            index++;
        }

        return urls;
    }

    /// <summary>Ensures every requested ID has exactly one selected manifest row.</summary>
    /// <param name="entries">The selected entries.</param>
    /// <param name="requestedIds">The requested IDs.</param>
    /// <exception cref="InvalidDataException">The manifest omits a requested ID.</exception>
    private static void EnsureAllIdsExist(Dictionary<string, CorpusEntry> entries, string[] requestedIds)
    {
        foreach (var id in requestedIds)
        {
            if (!entries.ContainsKey(id))
            {
                throw new InvalidDataException($"The corpus manifest does not contain requested ID {id}.");
            }
        }
    }

    /// <summary>Reads a required string-valued JSON property.</summary>
    /// <param name="element">The JSON object.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property value.</returns>
    /// <exception cref="InvalidDataException">The property is missing, not a string or null.</exception>
    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"A corpus entry is missing string property {name}.");
        }

        return property.GetString() ?? throw new InvalidDataException($"A corpus entry has null property {name}.");
    }

    /// <summary>Returns whether an ID can safely be used as one filename component.</summary>
    /// <param name="id">The candidate ID.</param>
    /// <returns><see langword="true"/> when the ID contains lowercase letters, digits and nonadjacent internal punctuation.</returns>
    private static bool IsSafeId(string id)
    {
        if (id.Length is < 1 or > MaximumIdLength || !IsAsciiAlphaNumeric(id[0]) || !IsAsciiAlphaNumeric(id[^1]))
        {
            return false;
        }

        foreach (var character in id)
        {
            if (!IsAsciiAlphaNumeric(character) && character is not '-' and not '.')
            {
                return false;
            }
        }

        return !id.Contains("..", StringComparison.Ordinal);
    }

    /// <summary>Returns whether a character is a lowercase ASCII letter or digit.</summary>
    /// <param name="character">The candidate character.</param>
    /// <returns><see langword="true"/> when the character is allowed in an ID.</returns>
    private static bool IsAsciiAlphaNumeric(char character) => character is >= 'a' and <= 'z' or >= '0' and <= '9';

    /// <summary>Checks that a string contains only hexadecimal characters.</summary>
    /// <param name="value">The candidate string.</param>
    /// <returns><see langword="true"/> when every character is hexadecimal.</returns>
    private static bool IsHex(string value)
    {
        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Rejects non-HTTPS URLs and URLs containing credentials.</summary>
    /// <param name="uri">The URL to validate.</param>
    /// <exception cref="InvalidDataException">The URL is not a safe HTTPS URL.</exception>
    private static void EnsureHttps(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidDataException("Corpus downloads and redirects must use HTTPS URLs without embedded credentials.");
        }
    }

    /// <summary>Reuses a verified cache entry or downloads and atomically installs it.</summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="entry">The expected corpus entry.</param>
    /// <param name="destination">The output directory.</param>
    /// <param name="cancellationToken">Cancels manifest-sized transfer work.</param>
    /// <returns>A task for the verified cache check or completed download.</returns>
    /// <exception cref="InvalidDataException">The response does not match the manifest.</exception>
    private static async Task FetchOneAsync(HttpClient client, CorpusEntry entry, string destination, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(Path.Combine(destination, entry.Id + DocumentExtension));
        EnsureInsideDirectory(path, destination);
        if (await IsVerifiedCacheAsync(path, entry, cancellationToken))
        {
            Console.WriteLine($"Verified cached {entry.Id}: {path}");
            return;
        }

        var temporaryPath = Path.Combine(destination, $".{entry.Id}.{Guid.NewGuid():N}.part");
        try
        {
            await DownloadWithRetryAsync(client, entry, temporaryPath, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
            Console.WriteLine($"Downloaded and verified {entry.Id}: {path}");
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    /// <summary>Confirms the generated output path remains inside the destination directory.</summary>
    /// <param name="path">The full output path.</param>
    /// <param name="destination">The full destination path.</param>
    /// <exception cref="InvalidDataException">The path escapes the destination directory.</exception>
    private static void EnsureInsideDirectory(string path, string destination)
    {
        var relative = Path.GetRelativePath(destination, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The destination path escaped its root.");
        }
    }

    /// <summary>Checks a cached file's type, exact size and SHA-256 digest.</summary>
    /// <param name="path">The cached file path.</param>
    /// <param name="entry">The expected corpus entry.</param>
    /// <param name="cancellationToken">Cancels the cache read.</param>
    /// <returns><see langword="true"/> when the cache entry matches the manifest.</returns>
    /// <exception cref="IOException">The cache path is a symbolic link or cannot be read.</exception>
    private static async Task<bool> IsVerifiedCacheAsync(string path, CorpusEntry entry, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"A cached corpus entry must not be a symbolic link: {path}");
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length != entry.ExpectedBytes)
        {
            return false;
        }

        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = await SHA256.HashDataAsync(input, cancellationToken);
        return CryptographicOperations.FixedTimeEquals(actualHash, entry.Sha256);
    }

    /// <summary>Removes a temporary download after success or failure.</summary>
    /// <param name="path">The temporary path.</param>
    private static void DeleteTemporaryFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>Retries transient network failures without accepting changed or corrupt fixture bytes.</summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="entry">The expected corpus entry.</param>
    /// <param name="temporaryPath">The temporary output path.</param>
    /// <param name="cancellationToken">Cancels the attempts and backoff.</param>
    /// <returns>A task for a verified download.</returns>
    private static async Task DownloadWithRetryAsync(HttpClient client, CorpusEntry entry, string temporaryPath, CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (true)
        {
            try
            {
                await DownloadAsync(client, entry, entry.Uris[(attempt - 1) % entry.Uris.Length], temporaryPath, cancellationToken);
                return;
            }
            catch (HttpRequestException error) when (attempt < MaximumDownloadAttempts && IsTransient(error) && !cancellationToken.IsCancellationRequested)
            {
                DeleteTemporaryFile(temporaryPath);
            }
            catch (IOException) when (attempt < MaximumDownloadAttempts && !cancellationToken.IsCancellationRequested)
            {
                DeleteTemporaryFile(temporaryPath);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(RetryDelayMilliseconds * attempt), cancellationToken);
            attempt++;
        }
    }

    /// <summary>Distinguishes connection and server failures from a permanent HTTP rejection.</summary>
    /// <param name="error">The failed request.</param>
    /// <returns>Whether another attempt may succeed.</returns>
    private static bool IsTransient(HttpRequestException error) => error.StatusCode is null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
        || error.StatusCode >= HttpStatusCode.InternalServerError;

    /// <summary>Follows bounded HTTPS redirects and streams the response into a temporary file.</summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="entry">The expected corpus entry.</param>
    /// <param name="source">The current validated HTTPS source.</param>
    /// <param name="temporaryPath">The temporary output path.</param>
    /// <param name="cancellationToken">Cancels network and file operations.</param>
    /// <returns>A task for the completed download.</returns>
    /// <exception cref="HttpRequestException">The request fails or exceeds the redirect limit.</exception>
    /// <exception cref="InvalidDataException">The response differs from the manifest.</exception>
    private static async Task DownloadAsync(HttpClient client, CorpusEntry entry, Uri source, string temporaryPath, CancellationToken cancellationToken)
    {
        var uri = source;
        var redirectCount = 0;
        while (true)
        {
            EnsureHttps(uri);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (IsRedirect(response.StatusCode))
            {
                uri = GetRedirectUri(response, uri, entry.Id, redirectCount);
                redirectCount++;
                continue;
            }

            _ = response.EnsureSuccessStatusCode();
            EnsureContentLength(response, entry);
            await SaveAndVerifyAsync(response, entry, temporaryPath, cancellationToken);
            return;
        }
    }

    /// <summary>Returns whether the status code carries an HTTP redirect.</summary>
    /// <param name="statusCode">The response status.</param>
    /// <returns><see langword="true"/> for supported redirect status codes.</returns>
    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or
        HttpStatusCode.Redirect or
        HttpStatusCode.SeeOther or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;

    /// <summary>Resolves and validates one redirect target.</summary>
    /// <param name="response">The redirect response.</param>
    /// <param name="currentUri">The current request URI.</param>
    /// <param name="id">The corpus entry ID.</param>
    /// <param name="redirectCount">The number of redirects already followed.</param>
    /// <returns>The validated next URI.</returns>
    /// <exception cref="HttpRequestException">The response has no location or exceeds the redirect limit.</exception>
    /// <exception cref="InvalidDataException">The redirect target does not use HTTPS.</exception>
    private static Uri GetRedirectUri(HttpResponseMessage response, Uri currentUri, string id, int redirectCount)
    {
        if (redirectCount >= MaximumRedirects || response.Headers.Location is not { } location)
        {
            throw new HttpRequestException($"The corpus download exceeded the redirect limit for {id}.");
        }

        var nextUri = ResolveRedirectUri(location, currentUri);
        EnsureHttps(nextUri);
        return nextUri;
    }

    /// <summary>Resolves a redirect location against the current request URI.</summary>
    /// <param name="location">The response location.</param>
    /// <param name="currentUri">The URI that received the response.</param>
    /// <returns>The absolute redirect URI.</returns>
    private static Uri ResolveRedirectUri(Uri location, Uri currentUri) => location.IsAbsoluteUri ? location : new(currentUri, location);

    /// <summary>Checks a declared response length against the manifest.</summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="entry">The expected corpus entry.</param>
    /// <exception cref="InvalidDataException">The declared length differs from the manifest.</exception>
    private static void EnsureContentLength(HttpResponseMessage response, CorpusEntry entry)
    {
        if (response.Content.Headers.ContentLength is { } contentLength && contentLength != entry.ExpectedBytes)
        {
            throw new InvalidDataException($"The download size for {entry.Id} differs from its manifest entry.");
        }
    }

    /// <summary>Streams a response to disk while checking its expected length and digest.</summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="entry">The expected corpus entry.</param>
    /// <param name="temporaryPath">The temporary output path.</param>
    /// <param name="cancellationToken">Cancels network and file operations.</param>
    /// <returns>A task for the verified transfer.</returns>
    /// <exception cref="InvalidDataException">The response length or digest differs from the manifest.</exception>
    private static async Task SaveAndVerifyAsync(HttpResponseMessage response, CorpusEntry entry, string temporaryPath, CancellationToken cancellationToken)
    {
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSize];
        long totalBytes = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
        {
            totalBytes = checked(totalBytes + read);
            if (totalBytes > entry.ExpectedBytes || totalBytes > MaximumDocumentBytes)
            {
                throw new InvalidDataException($"The download for {entry.Id} exceeds its manifest size.");
            }

            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        await output.FlushAsync(cancellationToken);
        var actualHash = hash.GetHashAndReset();
        if (totalBytes != entry.ExpectedBytes || !CryptographicOperations.FixedTimeEquals(actualHash, entry.Sha256))
        {
            throw new InvalidDataException($"The downloaded file for {entry.Id} does not match its manifest size and SHA-256.");
        }
    }

    /// <summary>A validated manifest row for one PDF fixture.</summary>
    /// <param name="Id">The safe fixture identifier.</param>
    /// <param name="Uris">The HTTPS source and explicit mirrors in attempt order.</param>
    /// <param name="Sha256">The expected digest bytes.</param>
    /// <param name="ExpectedBytes">The expected file length.</param>
    private sealed record CorpusEntry(string Id, Uri[] Uris, byte[] Sha256, long ExpectedBytes);
}
