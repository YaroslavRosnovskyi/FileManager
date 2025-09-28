using FileManager.Core.Interfaces;
using FileManager.Core.Models;
using System.Text.Json;

namespace FileManager.Server.Services;

public class StorageService : IStorageService
{
    private readonly ILogger<StorageService> _logger;
    private readonly IAuthService _authService;
    private readonly string _basePath;
    private readonly string _filesMetadataPath;
    private readonly Dictionary<string, FileItem> _fileMetadata = new();

    public StorageService(ILogger<StorageService> logger, IAuthService authService, IConfiguration configuration)
    {
        _logger = logger;
        _authService = authService;
        
        _basePath = configuration["StoragePaths:Base"] ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Storage");
        _filesMetadataPath = Path.Combine(_basePath, "metadata.json");
        
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
        
        LoadMetadata();
    }

    public async Task<IEnumerable<FileItem>> GetFilesAsync(string token, string folderPath, string? fileTypeFilter = null, string? sortBy = null, bool ascending = true)
    {
        var user = await _authService.GetUserFromTokenAsync(token);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid or expired token");
        }

        var files = _fileMetadata.Values.Where(f => f.Path == folderPath || string.IsNullOrEmpty(folderPath));

        if (!string.IsNullOrEmpty(fileTypeFilter))
        {
            files = files.Where(f => f.FileType.Equals(fileTypeFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(sortBy))
        {
            files = sortBy.ToLower() switch
            {
                "name" => ascending ? files.OrderBy(f => f.Name) : files.OrderByDescending(f => f.Name),
                "size" => ascending ? files.OrderBy(f => f.Size) : files.OrderByDescending(f => f.Size),
                "type" => ascending ? files.OrderBy(f => f.FileType) : files.OrderByDescending(f => f.FileType),
                "createdat" => ascending ? files.OrderBy(f => f.CreatedAt) : files.OrderByDescending(f => f.CreatedAt),
                "modifiedat" => ascending ? files.OrderBy(f => f.ModifiedAt) : files.OrderByDescending(f => f.ModifiedAt),
                "createdby" => ascending ? files.OrderBy(f => f.CreatedBy) : files.OrderByDescending(f => f.CreatedBy),
                "modifiedby" => ascending ? files.OrderBy(f => f.ModifiedBy) : files.OrderByDescending(f => f.ModifiedBy),
                _ => ascending ? files.OrderBy(f => f.Name) : files.OrderByDescending(f => f.Name)
            };
        }
        else
        {
            files = ascending ? files.OrderBy(f => f.Name) : files.OrderByDescending(f => f.Name);
        }

        return files.ToList();
    }

    public async Task<(string FileId, long FileSize)> SaveFileAsync(string token, string fileName, string folderPath, Stream fileStream)
    {
        var user = await _authService.GetUserFromTokenAsync(token);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid or expired token");
        }

        string fileId = Guid.NewGuid().ToString();
        
        string userStoragePath = Path.Combine(_basePath, "Files", user.Id);
        if (!Directory.Exists(userStoragePath))
        {
            Directory.CreateDirectory(userStoragePath);
        }

        string targetDirectory = Path.Combine(userStoragePath, folderPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        string filePath = Path.Combine(targetDirectory, fileId);
        
        using (var fileOutput = File.Create(filePath))
        {
            await fileStream.CopyToAsync(fileOutput);
        }

        var fileInfo = new FileInfo(filePath);
        long fileSize = fileInfo.Length;

        string fileExtension = Path.GetExtension(fileName).ToLowerInvariant();

        var fileItem = new FileItem
        {
            Id = fileId,
            Name = fileName,
            Path = folderPath,
            Size = fileSize,
            FileType = fileExtension,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            CreatedBy = user.Username,
            ModifiedBy = user.Username
        };

        _fileMetadata[fileId] = fileItem;
        SaveMetadata();

        _logger.LogInformation("File saved: {FileName}, Size: {FileSize} bytes, ID: {FileId}", fileName, fileSize, fileId);
        return (fileId, fileSize);
    }

    public async Task<(Stream FileStream, FileItem Metadata)> GetFileAsync(string token, string fileId)
    {
        var user = await _authService.GetUserFromTokenAsync(token);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid or expired token");
        }

        if (!_fileMetadata.TryGetValue(fileId, out var fileItem))
        {
            throw new FileNotFoundException($"File with ID {fileId} not found");
        }

        string userStoragePath = Path.Combine(_basePath, "Files", user.Id);
        string targetDirectory = Path.Combine(userStoragePath, fileItem.Path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        string filePath = Path.Combine(targetDirectory, fileId);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Physical file with ID {fileId} not found");
        }

        var fileStream = File.OpenRead(filePath);
        return (fileStream, fileItem);
    }

    public async Task<bool> DeleteFileAsync(string token, string fileId)
    {
        var user = await _authService.GetUserFromTokenAsync(token);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid or expired token");
        }

        if (!_fileMetadata.TryGetValue(fileId, out var fileItem))
        {
            throw new FileNotFoundException($"File with ID {fileId} not found");
        }

        string userStoragePath = Path.Combine(_basePath, "Files", user.Id);
        string targetDirectory = Path.Combine(userStoragePath, fileItem.Path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        string filePath = Path.Combine(targetDirectory, fileId);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        _fileMetadata.Remove(fileId);
        SaveMetadata();

        _logger.LogInformation("File deleted: {FileName}, ID: {FileId}", fileItem.Name, fileId);
        return true;
    }

    public async Task<SyncResult> SynchronizeFolderAsync(
        string token, 
        string localFolderPath, 
        string remoteFolderPath, 
        IProgress<SyncProgress>? progress = null)
    {
        var user = await _authService.GetUserFromTokenAsync(token);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid or expired token");
        }

        string userStoragePath = Path.Combine(_basePath, "Files", user.Id);
        string remoteDirectoryPath = Path.Combine(userStoragePath, remoteFolderPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        
        if (!Directory.Exists(remoteDirectoryPath))
        {
            Directory.CreateDirectory(remoteDirectoryPath);
        }

        if (!Directory.Exists(localFolderPath))
        {
            throw new DirectoryNotFoundException($"Local directory not found: {localFolderPath}");
        }

        var localFiles = Directory.GetFiles(localFolderPath, "*.*", SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .ToList();

        var remoteFiles = _fileMetadata.Values
            .Where(f => f.Path.StartsWith(remoteFolderPath))
            .ToList();

        int totalFiles = localFiles.Count + remoteFiles.Count;
        int processedCount = 0;
        int syncedCount = 0;
        int uploadedCount = 0;
        int downloadedCount = 0;
        int deletedCount = 0;

        progress?.Report(new SyncProgress
        {
            Status = SyncStatus.Syncing,
            Message = "Starting synchronization...",
            FilesProcessed = processedCount,
            FilesSynced = syncedCount,
            TotalFiles = totalFiles
        });

        progress?.Report(new SyncProgress
        {
            Status = SyncStatus.Syncing,
            Message = "Phase 1: Uploading local files to server...",
            FilesProcessed = processedCount,
            FilesSynced = syncedCount,
            TotalFiles = totalFiles
        });

        foreach (var localFile in localFiles)
        {
            processedCount++;
            
            string relativePath = Path.GetRelativePath(localFolderPath, localFile.DirectoryName ?? string.Empty);
            string remoteRelativePath = Path.Combine(remoteFolderPath, relativePath).Replace('\\', '/');
            if (remoteRelativePath.EndsWith("/"))
            {
                remoteRelativePath = remoteRelativePath[..^1];
            }

            var matchingRemoteFile = remoteFiles.FirstOrDefault(rf => 
                rf.Path == remoteRelativePath && 
                rf.Name == localFile.Name);

            bool shouldUpload = false;

            if (matchingRemoteFile == null)
            {
                shouldUpload = true;
            }
            else
            {
                if (localFile.LastWriteTimeUtc > matchingRemoteFile.ModifiedAt)
                {
                    shouldUpload = true;
                    
                    await DeleteFileAsync(token, matchingRemoteFile.Id);
                }
            }

            if (shouldUpload)
            {
                using var fileStream = localFile.OpenRead();
                await SaveFileAsync(token, localFile.Name, remoteRelativePath, fileStream);
                syncedCount++;
                uploadedCount++;
                
                progress?.Report(new SyncProgress
                {
                    Status = SyncStatus.Syncing,
                    Message = $"Uploaded: {localFile.Name}",
                    FilesProcessed = processedCount,
                    FilesSynced = syncedCount,
                    TotalFiles = totalFiles
                });
            }
            else
            {
                progress?.Report(new SyncProgress
                {
                    Status = SyncStatus.Syncing,
                    Message = $"Checked: {localFile.Name} (no upload needed)",
                    FilesProcessed = processedCount,
                    FilesSynced = syncedCount,
                    TotalFiles = totalFiles
                });
            }
        }

        progress?.Report(new SyncProgress
        {
            Status = SyncStatus.Syncing,
            Message = "Phase 2: Downloading server files to local folder...",
            FilesProcessed = processedCount,
            FilesSynced = syncedCount,
            TotalFiles = totalFiles
        });

        var refreshedRemoteFiles = _fileMetadata.Values
            .Where(f => f.Path.StartsWith(remoteFolderPath))
            .ToList();

        _logger.LogInformation("Phase 2: Found {Count} remote files to process", refreshedRemoteFiles.Count);
        foreach (var rf in refreshedRemoteFiles)
        {
            _logger.LogDebug("Remote file: {Name} in path: {Path}", rf.Name, rf.Path);
        }

        foreach (var remoteFile in refreshedRemoteFiles)
        {
            processedCount++;
            
            string relativeRemotePath = remoteFile.Path.TrimStart('/');
            string localFilePath;
            
            if (string.IsNullOrEmpty(relativeRemotePath) || relativeRemotePath == ".")
            {
                localFilePath = Path.Combine(localFolderPath, remoteFile.Name);
            }
            else
            {
                string localRelativePath = relativeRemotePath.Replace('/', Path.DirectorySeparatorChar);
                localFilePath = Path.Combine(localFolderPath, localRelativePath, remoteFile.Name);
            }
            
            string localFileDir = Path.GetDirectoryName(localFilePath) ?? localFolderPath;
            
            if (!Directory.Exists(localFileDir))
            {
                Directory.CreateDirectory(localFileDir);
            }
            
            bool shouldDownload = false;
            
            if (!File.Exists(localFilePath))
            {
                shouldDownload = true;
            }
            else
            {
                var localFileInfo = new FileInfo(localFilePath);
                if (remoteFile.ModifiedAt > localFileInfo.LastWriteTimeUtc)
                {
                    shouldDownload = true;
                }
            }
            
            if (shouldDownload)
            {
                try
                {
                    var (fileStream, metadata) = await GetFileAsync(token, remoteFile.Id);
                    
                    using (fileStream)
                    using (var outputFile = File.Create(localFilePath))
                    {
                        await fileStream.CopyToAsync(outputFile);
                    }
                    
                    File.SetLastWriteTimeUtc(localFilePath, remoteFile.ModifiedAt);
                    
                    syncedCount++;
                    downloadedCount++;
                    
                    progress?.Report(new SyncProgress
                    {
                        Status = SyncStatus.Syncing,
                        Message = $"Downloaded: {remoteFile.Name}",
                        FilesProcessed = processedCount,
                        FilesSynced = syncedCount,
                        TotalFiles = totalFiles
                    });
                    
                    _logger.LogInformation("Downloaded file: {FileName} to {LocalPath}", remoteFile.Name, localFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error downloading file {FileName}", remoteFile.Name);
                    
                    progress?.Report(new SyncProgress
                    {
                        Status = SyncStatus.Syncing,
                        Message = $"Error downloading: {remoteFile.Name}",
                        FilesProcessed = processedCount,
                        FilesSynced = syncedCount,
                        TotalFiles = totalFiles
                    });
                }
            }
            else
            {
                progress?.Report(new SyncProgress
                {
                    Status = SyncStatus.Syncing,
                    Message = $"Checked: {remoteFile.Name} (no download needed)",
                    FilesProcessed = processedCount,
                    FilesSynced = syncedCount,
                    TotalFiles = totalFiles
                });
            }
        }

        progress?.Report(new SyncProgress
        {
            Status = SyncStatus.Syncing,
            Message = "Phase 3: Cleaning up deleted files from local folder...",
            FilesProcessed = processedCount,
            FilesSynced = syncedCount,
            TotalFiles = totalFiles
        });

        var currentLocalFiles = Directory.GetFiles(localFolderPath, "*.*", SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .ToList();

        foreach (var localFile in currentLocalFiles)
        {
            string relativePath = Path.GetRelativePath(localFolderPath, localFile.DirectoryName ?? string.Empty);
            string remoteRelativePath;
            
            if (string.IsNullOrEmpty(relativePath) || relativePath == ".")
            {
                remoteRelativePath = remoteFolderPath;
            }
            else
            {
                remoteRelativePath = Path.Combine(remoteFolderPath, relativePath).Replace('\\', '/');
            }
            
            if (remoteRelativePath.EndsWith("/") && remoteRelativePath.Length > 1)
            {
                remoteRelativePath = remoteRelativePath[..^1];
            }

            var matchingRemoteFile = refreshedRemoteFiles.FirstOrDefault(rf => 
                rf.Path == remoteRelativePath && 
                rf.Name == localFile.Name);

            if (matchingRemoteFile == null)
            {
                try
                {
                    File.Delete(localFile.FullName);
                    deletedCount++;
                    syncedCount++;
                    
                    progress?.Report(new SyncProgress
                    {
                        Status = SyncStatus.Syncing,
                        Message = $"Deleted local file: {localFile.Name} (no longer exists on server)",
                        FilesProcessed = processedCount,
                        FilesSynced = syncedCount,
                        TotalFiles = totalFiles
                    });
                    
                    _logger.LogInformation("Deleted local file: {FileName} (no longer exists on server)", localFile.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting local file {FileName}", localFile.Name);
                    
                    progress?.Report(new SyncProgress
                    {
                        Status = SyncStatus.Syncing,
                        Message = $"Error deleting: {localFile.Name}",
                        FilesProcessed = processedCount,
                        FilesSynced = syncedCount,
                        TotalFiles = totalFiles
                    });
                }
            }
        }

        try
        {
            CleanupEmptyDirectories(localFolderPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up empty directories");
        }

        string completionMessage = $"Synchronization completed! " +
                                 $"Uploaded: {uploadedCount}, Downloaded: {downloadedCount}, Deleted: {deletedCount}";

        progress?.Report(new SyncProgress
        {
            Status = SyncStatus.Completed,
            Message = completionMessage,
            FilesProcessed = processedCount,
            FilesSynced = syncedCount,
            TotalFiles = totalFiles
        });

        _logger.LogInformation("Sync completed - Uploaded: {Uploaded}, Downloaded: {Downloaded}, Deleted: {Deleted}", 
            uploadedCount, downloadedCount, deletedCount);

        return new SyncResult
        {
            Success = true,
            FilesProcessed = processedCount,
            FilesSynced = syncedCount,
            TotalFiles = totalFiles
        };
    }

    private void CleanupEmptyDirectories(string directoryPath)
    {
        try
        {
            var subdirectories = Directory.GetDirectories(directoryPath);
            
            foreach (var subdirectory in subdirectories)
            {
                CleanupEmptyDirectories(subdirectory);
            }
            
            if (Directory.GetFiles(directoryPath).Length == 0 && 
                Directory.GetDirectories(directoryPath).Length == 0 &&
                !string.Equals(directoryPath, Path.GetFullPath(directoryPath), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Directory.Delete(directoryPath);
                    _logger.LogInformation("Cleaned up empty directory: {DirectoryPath}", directoryPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete empty directory: {DirectoryPath}", directoryPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during directory cleanup: {DirectoryPath}", directoryPath);
        }
    }

    private void LoadMetadata()
    {
        if (File.Exists(_filesMetadataPath))
        {
            try
            {
                string json = File.ReadAllText(_filesMetadataPath);
                var metadata = JsonSerializer.Deserialize<List<FileItem>>(json);
                if (metadata != null)
                {
                    _fileMetadata.Clear();
                    foreach (var item in metadata)
                    {
                        _fileMetadata[item.Id] = item;
                    }
                    _logger.LogInformation("Loaded {Count} file metadata items", _fileMetadata.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading file metadata");
            }
        }
    }

    private void SaveMetadata()
    {
        try
        {
            var metadata = _fileMetadata.Values.ToList();
            string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filesMetadataPath, json);
            _logger.LogInformation("Saved {Count} file metadata items", metadata.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file metadata");
        }
    }
}