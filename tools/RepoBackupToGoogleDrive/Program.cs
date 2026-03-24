using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var options = BackupOptions.Parse(args, Directory.GetCurrentDirectory());

if (!File.Exists(options.ConfigPath))
{
    Console.Error.WriteLine($"Config file was not found: {options.ConfigPath}");
    return 1;
}

if (!Directory.Exists(options.RepoRoot))
{
    Console.Error.WriteLine($"Repo root was not found: {options.RepoRoot}");
    return 1;
}

var config = AppConfig.Load(options.ConfigPath);
var credentialsPath = Path.IsPathRooted(config.GoogleDrive.CredentialsJsonPath)
    ? config.GoogleDrive.CredentialsJsonPath
    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(options.ConfigPath)!, config.GoogleDrive.CredentialsJsonPath));

if (!File.Exists(credentialsPath))
{
    Console.Error.WriteLine($"Google Drive credentials JSON was not found: {credentialsPath}");
    Console.Error.WriteLine("Create it first or place it at the configured path.");
    return 1;
}

var credentials = ServiceAccountCredentials.Load(credentialsPath);
var repoDirectories = Directory.GetDirectories(options.RepoRoot)
    .Where(path => Directory.Exists(Path.Combine(path, ".git")))
    .Where(path => options.RepoNames.Count == 0 || options.RepoNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToList();

if (repoDirectories.Count == 0)
{
    Console.Error.WriteLine("No git repositories were found to back up.");
    return 1;
}

var tempRoot = Path.Combine(Path.GetTempPath(), "RepoBackups", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
Directory.CreateDirectory(tempRoot);

try
{
    using var httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    var accessToken = await GoogleAuthClient.GetAccessTokenAsync(httpClient, credentials, CancellationToken.None);
    var driveClient = new GoogleDriveClient(httpClient, accessToken);

    var backupsFolderId = await driveClient.EnsureFolderAsync(config.GoogleDrive.ParentFolderId, options.BackupFolderName, CancellationToken.None);

    foreach (var repoPath in repoDirectories)
    {
        var repoName = Path.GetFileName(repoPath);
        Console.WriteLine($"Backing up {repoName}...");

        var repoBackupFolderId = await driveClient.EnsureFolderAsync(backupsFolderId, repoName, CancellationToken.None);
        var archiveName = $"{repoName}-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
        var archivePath = Path.Combine(tempRoot, archiveName);

        RepoArchiver.CreateArchive(repoPath, archivePath);
        await driveClient.UploadFileAsync(repoBackupFolderId, archiveName, archivePath, CancellationToken.None);

        Console.WriteLine($"Uploaded {archiveName}");
    }

    Console.WriteLine("Repo backup upload completed.");
    return 0;
}
finally
{
    if (!options.KeepLocalArchives && Directory.Exists(tempRoot))
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
            // Ignore temp cleanup failures.
        }
    }
}

internal sealed class BackupOptions
{
    public string ConfigPath { get; set; } = string.Empty;
    public string RepoRoot { get; set; } = string.Empty;
    public string BackupFolderName { get; set; } = "Repo Backups";
    public bool KeepLocalArchives { get; set; }
    public List<string> RepoNames { get; } = new();

    public static BackupOptions Parse(string[] args, string currentDirectory)
    {
        var options = new BackupOptions
        {
            ConfigPath = Path.GetFullPath(Path.Combine(currentDirectory, "appsettings.json")),
            RepoRoot = Path.GetFullPath(Path.Combine(currentDirectory, ".."))
        };

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config":
                    options.ConfigPath = Path.GetFullPath(args[++i]);
                    break;
                case "--repo-root":
                    options.RepoRoot = Path.GetFullPath(args[++i]);
                    break;
                case "--backup-folder":
                    options.BackupFolderName = args[++i];
                    break;
                case "--keep-local":
                    options.KeepLocalArchives = true;
                    break;
                case "--repo":
                    options.RepoNames.Add(args[++i]);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown argument: {args[i]}");
            }
        }

        return options;
    }
}

internal sealed class AppConfig
{
    public GoogleDriveConfig GoogleDrive { get; init; } = new();

    public static AppConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Default);
        return config ?? new AppConfig();
    }
}

internal sealed class GoogleDriveConfig
{
    public string ParentFolderId { get; init; } = string.Empty;
    public string CredentialsJsonPath { get; init; } = "secrets/google-drive-service-account.json";
}

internal sealed class ServiceAccountCredentials
{
    public string ClientEmail { get; init; } = string.Empty;
    public string PrivateKey { get; init; } = string.Empty;
    public string TokenUri { get; init; } = "https://oauth2.googleapis.com/token";

    public static ServiceAccountCredentials Load(string path)
    {
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new ServiceAccountCredentials
        {
            ClientEmail = root.GetProperty("client_email").GetString() ?? string.Empty,
            PrivateKey = root.GetProperty("private_key").GetString() ?? string.Empty,
            TokenUri = root.TryGetProperty("token_uri", out var tokenUri)
                ? tokenUri.GetString() ?? "https://oauth2.googleapis.com/token"
                : "https://oauth2.googleapis.com/token"
        };
    }
}

internal static class RepoArchiver
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        "bin",
        "obj",
        "artifacts",
        "node_modules",
        "build-verify"
    };

    private static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "google-drive-service-account.json",
        "live-app.out.log",
        "live-app.err.log",
        "run.out.log",
        "app-run.log",
        "app-run.err",
        "cookie.txt",
        "Output-Build.txt"
    };

    private static readonly string[] ExcludedExtensions =
    [
        ".log",
        ".db",
        ".suo",
        ".wsuo",
        ".user",
        ".cache"
    ];

    public static void CreateArchive(string repoPath, string archivePath)
    {
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (var filePath in EnumerateIncludedFiles(repoPath))
        {
            var entryName = Path.GetRelativePath(repoPath, filePath).Replace('\\', '/');
            archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
        }
    }

    private static IEnumerable<string> EnumerateIncludedFiles(string repoPath)
    {
        var stack = new Stack<string>();
        stack.Push(repoPath);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            foreach (var directory in Directory.GetDirectories(current))
            {
                var directoryName = Path.GetFileName(directory);
                if (ExcludedDirectoryNames.Contains(directoryName))
                {
                    continue;
                }

                if (directory.Contains($"{Path.DirectorySeparatorChar}.github{Path.DirectorySeparatorChar}build-verify", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                stack.Push(directory);
            }

            foreach (var filePath in Directory.GetFiles(current))
            {
                var fileName = Path.GetFileName(filePath);
                if (ExcludedFileNames.Contains(fileName))
                {
                    continue;
                }

                if (ExcludedExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return filePath;
            }
        }
    }
}

internal sealed class GoogleDriveClient
{
    private readonly HttpClient _httpClient;

    public GoogleDriveClient(HttpClient httpClient, string accessToken)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<string> EnsureFolderAsync(string parentFolderId, string folderName, CancellationToken cancellationToken)
    {
        var existingFolderId = await FindFolderAsync(parentFolderId, folderName, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingFolderId))
        {
            return existingFolderId;
        }

        var payload = new
        {
            name = folderName,
            mimeType = "application/vnd.google-apps.folder",
            parents = new[] { parentFolderId }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/drive/v3/files?supportsAllDrives=true")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions.Default), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("Drive folder id was missing.");
    }

    public async Task UploadFileAsync(string parentFolderId, string fileName, string filePath, CancellationToken cancellationToken)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            name = fileName,
            parents = new[] { parentFolderId }
        }, JsonOptions.Default);

        using var multipart = new MultipartContent("related", $"backup_{Guid.NewGuid():N}");
        var metadataContent = new StringContent(metadata, Encoding.UTF8, "application/json");
        multipart.Add(metadataContent);

        var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        multipart.Add(fileContent);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&supportsAllDrives=true")
        {
            Content = multipart
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    private async Task<string?> FindFolderAsync(string parentFolderId, string folderName, CancellationToken cancellationToken)
    {
        var query = $"name = '{EscapeQueryValue(folderName)}' and mimeType = 'application/vnd.google-apps.folder' and '{EscapeQueryValue(parentFolderId)}' in parents and trashed = false";
        var url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name)&includeItemsFromAllDrives=true&supportsAllDrives=true";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("files", out var files) || files.GetArrayLength() == 0)
        {
            return null;
        }

        return files[0].GetProperty("id").GetString();
    }

    private static string EscapeQueryValue(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Google Drive request failed: {(int)response.StatusCode} {response.ReasonPhrase}{Environment.NewLine}{body}");
    }
}

internal static class GoogleAuthClient
{
    public static async Task<string> GetAccessTokenAsync(HttpClient httpClient, ServiceAccountCredentials credentials, CancellationToken cancellationToken)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(credentials.PrivateKey);

        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddMinutes(55);

        var header = Base64UrlEncode("""{"alg":"RS256","typ":"JWT"}""");
        var payload = Base64UrlEncode(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["iss"] = credentials.ClientEmail,
            ["scope"] = "https://www.googleapis.com/auth/drive",
            ["aud"] = credentials.TokenUri,
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds()
        }, JsonOptions.Default));

        var unsignedToken = $"{header}.{payload}";
        var signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(unsignedToken), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signedToken = $"{unsignedToken}.{Base64UrlEncode(signatureBytes)}";

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, credentials.TokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = signedToken
            })
        };

        using var response = await httpClient.SendAsync(tokenRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to acquire Google Drive access token: {(int)response.StatusCode} {response.ReasonPhrase}{Environment.NewLine}{body}");
        }

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Google token response did not include access_token.");
    }

    private static string Base64UrlEncode(string value)
        => Base64UrlEncode(Encoding.UTF8.GetBytes(value));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

internal static class JsonOptions
{
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
