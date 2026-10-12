// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace HyperPdf.Core.Settings;

/// <summary>Reads and atomically writes a JSON file that only its owner can read.</summary>
internal static class PrivateJsonFile
{
    /// <summary>Reads a file.</summary>
    /// <typeparam name="T">The stored type.</typeparam>
    /// <param name="path">The file.</param>
    /// <param name="type">The generated JSON metadata.</param>
    /// <returns>The value, or <see langword="null"/> when the file is missing or unreadable.</returns>
    internal static T? Load<T>(string path, JsonTypeInfo<T> type)
        where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, type);
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Writes a file atomically, readable only by its owner.</summary>
    /// <typeparam name="T">The stored type.</typeparam>
    /// <param name="path">The file.</param>
    /// <param name="value">The value.</param>
    /// <param name="type">The generated JSON metadata.</param>
    internal static void Save<T>(string path, T value, JsonTypeInfo<T> type)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        var temp = $"{path}.tmp";

        // The files can hold the person's Azure Speech key and signatures, so only their owner can read them.
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        File.Delete(temp);
        using (var stream = new FileStream(temp, options))
        {
            JsonSerializer.Serialize(stream, value, type);
        }

        File.Move(temp, path, true);
    }
}
