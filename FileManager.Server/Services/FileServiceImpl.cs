using FileManager.Core.Interfaces;
using FileManager.Core.Models;
using FileManager.Proto;
using Google.Protobuf;
using Grpc.Core;

namespace FileManager.Server.Services;

public class FileServiceImpl : FileService.FileServiceBase
{
    private readonly ILogger<FileServiceImpl> _logger;
    private readonly IAuthService _authService;
    private readonly IStorageService _storageService;

    public FileServiceImpl(
        ILogger<FileServiceImpl> logger,
        IAuthService authService,
        IStorageService storageService)
    {
        _logger = logger;
        _authService = authService;
        _storageService = storageService;
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Login attempt for user: {Username}", request.Username);
        var result = await _authService.LoginAsync(request.Username, request.Password);
        
        return new LoginResponse
        {
            Success = result.Success,
            Token = result.Token,
            ErrorMessage = result.ErrorMessage
        };
    }

    public override async Task<LogoutResponse> Logout(LogoutRequest request, ServerCallContext context)
    {
        var success = await _authService.LogoutAsync(request.Token);
        return new LogoutResponse { Success = success };
    }

    public override async Task<ListFilesResponse> ListFiles(ListFilesRequest request, ServerCallContext context)
    {
        var response = new ListFilesResponse();
        
        try
        {
            var files = await _storageService.GetFilesAsync(
                request.Token,
                request.FolderPath,
                request.FileTypeFilter,
                request.SortBy,
                request.Ascending);
            
            foreach (var file in files)
            {
                response.Files.Add(new Proto.FileInfo
                {
                    Id = file.Id,
                    Name = file.Name,
                    Path = file.Path,
                    Size = file.Size,
                    FileType = file.FileType,
                    CreatedAt = file.CreatedAt.ToString("o"),
                    ModifiedAt = file.ModifiedAt.ToString("o"),
                    CreatedBy = file.CreatedBy,
                    ModifiedBy = file.ModifiedBy
                });
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access when listing files");
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files");
            throw new RpcException(new Status(StatusCode.Internal, $"Error listing files: {ex.Message}"));
        }
        
        return response;
    }

    public override async Task<CheckFileConflictResponse> CheckFileConflict(CheckFileConflictRequest request, ServerCallContext context)
    {
        var response = new CheckFileConflictResponse();
        
        try
        {
            var user = await _authService.GetUserFromTokenAsync(request.Token);
            if (user == null)
            {
                response.HasConflict = false;
                response.ErrorMessage = "Invalid or expired token";
                return response;
            }

            var existingFiles = await _storageService.GetFilesAsync(
                request.Token,
                request.FolderPath,
                null,
                "name",
                true);
                
            var conflictingFiles = existingFiles
                .Where(f => f.Name.Equals(request.FileName, StringComparison.OrdinalIgnoreCase) && 
                           f.Path.Equals(request.FolderPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            response.HasConflict = conflictingFiles.Any();
            
            foreach (var file in conflictingFiles)
            {
                response.ConflictingFiles.Add(new ConflictingFile
                {
                    Id = file.Id,
                    Name = file.Name,
                    Path = file.Path,
                    Size = file.Size,
                    FileType = file.FileType,
                    CreatedAt = file.CreatedAt.ToString("o"),
                    ModifiedAt = file.ModifiedAt.ToString("o"),
                    CreatedBy = file.CreatedBy,
                    ModifiedBy = file.ModifiedBy
                });
            }
            
            _logger.LogInformation("File conflict check for {FileName}: {HasConflict}", request.FileName, response.HasConflict);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access when checking file conflict");
            response.HasConflict = false;
            response.ErrorMessage = $"Unauthorized: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file conflict");
            response.HasConflict = false;
            response.ErrorMessage = $"Error checking file conflict: {ex.Message}";
        }
        
        return response;
    }

    public override async Task<FileUploadResponse> UploadFile(IAsyncStreamReader<FileUploadRequest> requestStream, ServerCallContext context)
    {
        FileMetadata? metadata = null;
        bool overwriteExisting = false;
        string? overwriteFileId = null;
        using var memoryStream = new MemoryStream();
        
        try
        {
            while (await requestStream.MoveNext())
            {
                var request = requestStream.Current;
                
                if (request.DataCase == FileUploadRequest.DataOneofCase.Metadata)
                {
                    metadata = request.Metadata;
                    overwriteExisting = request.OverwriteExisting;
                    overwriteFileId = string.IsNullOrEmpty(request.OverwriteFileId) ? null : request.OverwriteFileId;
                    _logger.LogInformation("Received file metadata: {FileName}, Size: {Size}, Overwrite: {Overwrite}", 
                        metadata.FileName, metadata.TotalSize, overwriteExisting);
                }
                else if (request.DataCase == FileUploadRequest.DataOneofCase.ChunkData)
                {
                    request.ChunkData.WriteTo(memoryStream);
                }
            }
            
            if (metadata == null)
            {
                return new FileUploadResponse
                {
                    Success = false,
                    ErrorMessage = "No file metadata received"
                };
            }
            
            memoryStream.Position = 0;
            
            if (overwriteExisting && !string.IsNullOrEmpty(overwriteFileId))
            {
                try
                {
                    await _storageService.DeleteFileAsync(metadata.Token, overwriteFileId);
                    _logger.LogInformation("Deleted existing file {FileId} for overwrite", overwriteFileId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete existing file {FileId} for overwrite", overwriteFileId);
                }
            }
            
            var (fileId, _) = await _storageService.SaveFileAsync(
                metadata.Token,
                metadata.FileName,
                metadata.FolderPath,
                memoryStream);
            
            return new FileUploadResponse
            {
                Success = true,
                FileId = fileId
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access when uploading file");
            return new FileUploadResponse
            {
                Success = false,
                ErrorMessage = $"Unauthorized: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file upload");
            return new FileUploadResponse
            {
                Success = false,
                ErrorMessage = $"Error during file upload: {ex.Message}"
            };
        }
    }

    public override async Task DownloadFile(FileDownloadRequest request, IServerStreamWriter<FileDownloadResponse> responseStream, ServerCallContext context)
    {
        try
        {
            var (fileStream, metadata) = await _storageService.GetFileAsync(request.Token, request.FileId);
            
            using (fileStream)
            {
                await responseStream.WriteAsync(new FileDownloadResponse
                {
                    Metadata = new FileMetadata
                    {
                        FileName = metadata.Name,
                        TotalSize = metadata.Size
                    }
                });
                
                byte[] buffer = new byte[64 * 1024];
                int bytesRead;
                
                while ((bytesRead = await fileStream.ReadAsync(buffer)) > 0)
                {
                    await responseStream.WriteAsync(new FileDownloadResponse
                    {
                        ChunkData = ByteString.CopyFrom(buffer, 0, bytesRead)
                    });
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access when downloading file");
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "File not found when downloading");
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file download");
            throw new RpcException(new Status(StatusCode.Internal, $"Error during file download: {ex.Message}"));
        }
    }

    public override async Task<DeleteFileResponse> DeleteFile(DeleteFileRequest request, ServerCallContext context)
    {
        try
        {
            bool success = await _storageService.DeleteFileAsync(request.Token, request.FileId);
            
            return new DeleteFileResponse
            {
                Success = success
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access when deleting file");
            return new DeleteFileResponse
            {
                Success = false,
                ErrorMessage = $"Unauthorized: {ex.Message}"
            };
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "File not found when deleting");
            return new DeleteFileResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file");
            return new DeleteFileResponse
            {
                Success = false,
                ErrorMessage = $"Error deleting file: {ex.Message}"
            };
        }
    }

    public override async Task<GetSyncFileListResponse> GetSyncFileList(GetSyncFileListRequest request, ServerCallContext context)
    {
        var response = new GetSyncFileListResponse();
        
        try
        {
            var user = await _authService.GetUserFromTokenAsync(request.Token);
            if (user == null)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired token"));
            }
            
            var files = await _storageService.GetFilesAsync(
                request.Token,
                request.RemoteFolderPath,
                null,
                "name",
                true);
            
            foreach (var file in files)
            {
                response.Files.Add(new SyncFileInfo
                {
                    Name = file.Name,
                    Path = file.Path,
                    Size = file.Size,
                    ModifiedAt = file.ModifiedAt.ToString("o"),
                    FileId = file.Id
                });
            }
            
            _logger.LogInformation("Retrieved {Count} files for sync from folder: {FolderPath}", response.Files.Count, request.RemoteFolderPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access when getting sync file list");
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync file list");
            throw new RpcException(new Status(StatusCode.Internal, $"Error getting sync file list: {ex.Message}"));
        }
        
        return response;
    }

    public override async Task<SyncFileUploadResponse> SyncUploadFile(IAsyncStreamReader<SyncFileUploadRequest> requestStream, ServerCallContext context)
    {
        SyncFileMetadata? metadata = null;
        using var memoryStream = new MemoryStream();
        
        try
        {
            while (await requestStream.MoveNext())
            {
                var request = requestStream.Current;
                
                if (request.DataCase == SyncFileUploadRequest.DataOneofCase.Metadata)
                {
                    metadata = request.Metadata;
                    _logger.LogInformation("Received sync file metadata: {FileName}, Size: {Size}", metadata.FileName, metadata.TotalSize);
                }
                else if (request.DataCase == SyncFileUploadRequest.DataOneofCase.ChunkData)
                {
                    request.ChunkData.WriteTo(memoryStream);
                }
            }
            
            if (metadata == null)
            {
                return new SyncFileUploadResponse
                {
                    Success = false,
                    ErrorMessage = "No file metadata received"
                };
            }
            
            memoryStream.Position = 0;
            
            var existingFiles = await _storageService.GetFilesAsync(
                metadata.Token,
                metadata.FolderPath,
                null,
                "name",
                true);
                
            var existingFile = existingFiles.FirstOrDefault(f => f.Name == metadata.FileName && f.Path == metadata.FolderPath);
            
            DateTime localModifiedAt = DateTime.Parse(metadata.LocalModifiedAt);
            
            bool shouldUpload = true;
            if (existingFile != null)
            {
                if (localModifiedAt <= existingFile.ModifiedAt)
                {
                    shouldUpload = false;
                    _logger.LogInformation("Skipping upload of {FileName} - server version is newer or same", metadata.FileName);
                }
                else
                {
                    await _storageService.DeleteFileAsync(metadata.Token, existingFile.Id);
                    _logger.LogInformation("Replacing existing file {FileName} with newer local version", metadata.FileName);
                }
            }
            
            if (shouldUpload)
            {
                var (fileId, _) = await _storageService.SaveFileAsync(
                    metadata.Token,
                    metadata.FileName,
                    metadata.FolderPath,
                    memoryStream);
                
                return new SyncFileUploadResponse
                {
                    Success = true,
                    FileId = fileId
                };
            }
            else
            {
                return new SyncFileUploadResponse
                {
                    Success = true,
                    FileId = existingFile?.Id ?? string.Empty,
                    ErrorMessage = "File skipped - server version is newer"
                };
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access when uploading sync file");
            return new SyncFileUploadResponse
            {
                Success = false,
                ErrorMessage = $"Unauthorized: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync file upload");
            return new SyncFileUploadResponse
            {
                Success = false,
                ErrorMessage = $"Error during sync file upload: {ex.Message}"
            };
        }
    }

    public override async Task SynchronizeFolder(SyncFolderRequest request, IServerStreamWriter<SyncFolderResponse> responseStream, ServerCallContext context)
    {
        try
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (environment == "Docker")
            {
                await responseStream.WriteAsync(new SyncFolderResponse
                {
                    Status = SyncFolderResponse.Types.SyncStatus.Failed,
                    Message = "Folder synchronization is not supported when server runs in Docker. Please use the client applications' sync functionality instead."
                });
                return;
            }

            var progress = new Progress<SyncProgress>(async syncProgress =>
            {
                try
                {
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Synchronization cancelled by client");
                        return;
                    }

                    await responseStream.WriteAsync(new SyncFolderResponse
                    {
                        Status = syncProgress.Status switch
                        {
                            SyncStatus.Syncing => SyncFolderResponse.Types.SyncStatus.Syncing,
                            SyncStatus.Completed => SyncFolderResponse.Types.SyncStatus.Completed,
                            SyncStatus.Failed => SyncFolderResponse.Types.SyncStatus.Failed,
                            _ => SyncFolderResponse.Types.SyncStatus.Syncing
                        },
                        Message = syncProgress.Message,
                        FilesProcessed = syncProgress.FilesProcessed,
                        FilesSynced = syncProgress.FilesSynced,
                        TotalFiles = syncProgress.TotalFiles
                    });
                }
                catch (Exception ex) when (IsConnectionAborted(ex))
                {
                    _logger.LogInformation("Client disconnected during folder synchronization: {Message}", ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending synchronization progress update");
                }
            });
            
            await _storageService.SynchronizeFolderAsync(
                request.Token,
                request.LocalFolderPath,
                request.RemoteFolderPath,
                progress);
                
            try
            {
                if (!context.CancellationToken.IsCancellationRequested)
                {
                    await responseStream.WriteAsync(new SyncFolderResponse
                    {
                        Status = SyncFolderResponse.Types.SyncStatus.Completed,
                        Message = "Synchronization completed successfully"
                    });
                }
            }
            catch (Exception ex) when (IsConnectionAborted(ex))
            {
                _logger.LogInformation("Client disconnected before final synchronization message could be sent");
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access when synchronizing folder");
            try
            {
                if (!context.CancellationToken.IsCancellationRequested)
                {
                    await responseStream.WriteAsync(new SyncFolderResponse
                    {
                        Status = SyncFolderResponse.Types.SyncStatus.Failed,
                        Message = $"Unauthorized: {ex.Message}"
                    });
                }
            }
            catch (Exception streamEx) when (IsConnectionAborted(streamEx))
            {
                _logger.LogInformation("Client disconnected before error message could be sent");
            }
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogWarning(ex, "Directory not found when synchronizing");
            try
            {
                if (!context.CancellationToken.IsCancellationRequested)
                {
                    await responseStream.WriteAsync(new SyncFolderResponse
                    {
                        Status = SyncFolderResponse.Types.SyncStatus.Failed,
                        Message = ex.Message
                    });
                }
            }
            catch (Exception streamEx) when (IsConnectionAborted(streamEx))
            {
                _logger.LogInformation("Client disconnected before error message could be sent");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during folder synchronization");
            try
            {
                if (!context.CancellationToken.IsCancellationRequested)
                {
                    await responseStream.WriteAsync(new SyncFolderResponse
                    {
                        Status = SyncFolderResponse.Types.SyncStatus.Failed,
                        Message = $"Synchronization failed: {ex.Message}"
                    });
                }
            }
            catch (Exception streamEx) when (IsConnectionAborted(streamEx))
            {
                _logger.LogInformation("Client disconnected before error message could be sent");
            }
        }
    }
    
    private static bool IsConnectionAborted(Exception ex)
    {
        return ex is InvalidOperationException && ex.Message.Contains("request is complete") ||
               ex is RpcException rpcEx && (rpcEx.StatusCode == StatusCode.Cancelled || 
                                            rpcEx.StatusCode == StatusCode.Unavailable);
    }
}