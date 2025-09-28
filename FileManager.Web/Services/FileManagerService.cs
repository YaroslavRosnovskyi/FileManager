using FileManager.Core.Models;
using FileManager.Proto;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace FileManager.Web.Services;

public class FileManagerService : IFileManagerService, IDisposable
{
    private readonly FileService.FileServiceClient _client;
    private readonly GrpcChannel _channel;
    private string? _token;

    public bool IsLoggedIn => !string.IsNullOrEmpty(_token);
    public string? CurrentToken => _token;

    public FileManagerService(IConfiguration configuration)
    {
        string serverUrl = configuration["ServerConnection:GrpcUrl"] ?? "https://localhost:7121";
        
        var httpHandler = new HttpClientHandler();
        httpHandler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            
        var handler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, httpHandler);
        _channel = GrpcChannel.ForAddress(serverUrl, new GrpcChannelOptions
        {
            HttpHandler = handler,
            HttpVersion = new Version(2, 0)
        });
        
        _client = new FileService.FileServiceClient(_channel);
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var request = new LoginRequest
            {
                Username = username,
                Password = password
            };
            
            var response = await _client.LoginAsync(request);
            
            if (response.Success)
            {
                _token = response.Token;
                return true;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LogoutAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_token))
                return true;
            
            var request = new LogoutRequest { Token = _token };
            var response = await _client.LogoutAsync(request);
            
            if (response.Success)
            {
                _token = null;
                return true;
            }
            
            return false;
        }
        catch
        {
            _token = null;
            return true;
        }
    }

    public async Task<List<FileItem>> GetFilesAsync(string folderPath = "/", string fileTypeFilter = "", string sortBy = "name", bool ascending = true)
    {
        if (!IsLoggedIn)
            throw new InvalidOperationException("Not logged in");

        try
        {
            string serverSort = sortBy switch
            {
                "createdat" => "created",
                "modifiedat" => "modified", 
                "createdby" => "creator",
                "modifiedby" => "editor",
                _ => "name"
            };

            var request = new ListFilesRequest
            {
                Token = _token!,
                FolderPath = folderPath,
                FileTypeFilter = fileTypeFilter,
                SortBy = serverSort,
                Ascending = ascending
            };

            var response = await _client.ListFilesAsync(request);
            var files = new List<FileItem>();

            foreach (var fileInfo in response.Files)
            {
                files.Add(new FileItem
                {
                    Id = fileInfo.Id,
                    Name = fileInfo.Name,
                    Path = fileInfo.Path,
                    Size = fileInfo.Size,
                    FileType = fileInfo.FileType,
                    CreatedAt = DateTime.Parse(fileInfo.CreatedAt),
                    ModifiedAt = DateTime.Parse(fileInfo.ModifiedAt),
                    CreatedBy = fileInfo.CreatedBy,
                    ModifiedBy = fileInfo.ModifiedBy
                });
            }

            return sortBy switch
            {
                "size" => ascending ? files.OrderBy(f => f.Size).ToList() : files.OrderByDescending(f => f.Size).ToList(),
                "createdat" => ascending ? files.OrderBy(f => f.CreatedAt).ToList() : files.OrderByDescending(f => f.CreatedAt).ToList(),
                "modifiedat" => ascending ? files.OrderBy(f => f.ModifiedAt).ToList() : files.OrderByDescending(f => f.ModifiedAt).ToList(),
                "createdby" => ascending ? files.OrderBy(f => f.CreatedBy).ToList() : files.OrderByDescending(f => f.CreatedBy).ToList(),
                "modifiedby" => ascending ? files.OrderBy(f => f.ModifiedBy).ToList() : files.OrderByDescending(f => f.ModifiedBy).ToList(),
                _ => ascending ? files.OrderBy(f => f.Name).ToList() : files.OrderByDescending(f => f.Name).ToList()
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get files: {ex.Message}", ex);
        }
    }

    public async Task<(bool HasConflict, List<FileItem> ConflictingFiles)> CheckFileConflictAsync(string fileName, long fileSize, string folderPath = "/")
    {
        if (!IsLoggedIn)
            throw new InvalidOperationException("Not logged in");

        try
        {
            var request = new CheckFileConflictRequest
            {
                Token = _token!,
                FileName = fileName,
                FolderPath = folderPath,
                FileSize = fileSize
            };

            var response = await _client.CheckFileConflictAsync(request);
            
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                throw new InvalidOperationException(response.ErrorMessage);
            }

            var conflictingFiles = new List<FileItem>();
            foreach (var conflictingFile in response.ConflictingFiles)
            {
                conflictingFiles.Add(new FileItem
                {
                    Id = conflictingFile.Id,
                    Name = conflictingFile.Name,
                    Path = conflictingFile.Path,
                    Size = conflictingFile.Size,
                    FileType = conflictingFile.FileType,
                    CreatedAt = DateTime.Parse(conflictingFile.CreatedAt),
                    ModifiedAt = DateTime.Parse(conflictingFile.ModifiedAt),
                    CreatedBy = conflictingFile.CreatedBy,
                    ModifiedBy = conflictingFile.ModifiedBy
                });
            }

            return (response.HasConflict, conflictingFiles);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to check file conflict: {ex.Message}", ex);
        }
    }

    public async Task<bool> UploadFileAsync(string fileName, byte[] content, string folderPath = "/", IProgress<int>? progress = null, bool overwriteExisting = false, string? overwriteFileId = null)
    {
        if (!IsLoggedIn)
            throw new InvalidOperationException("Not logged in");

        try
        {
            using var memoryStream = new MemoryStream(content);
            using var call = _client.UploadFile();
            
            await call.RequestStream.WriteAsync(new FileUploadRequest
            {
                Metadata = new FileMetadata
                {
                    Token = _token!,
                    FileName = fileName,
                    FolderPath = folderPath,
                    TotalSize = content.Length
                },
                OverwriteExisting = overwriteExisting,
                OverwriteFileId = overwriteFileId ?? string.Empty
            });
            
            byte[] buffer = new byte[64 * 1024];
            int bytesRead;
            long totalBytesRead = 0;
            
            memoryStream.Position = 0;
            while ((bytesRead = await memoryStream.ReadAsync(buffer)) > 0)
            {
                await call.RequestStream.WriteAsync(new FileUploadRequest
                {
                    ChunkData = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead)
                });
                
                totalBytesRead += bytesRead;
                progress?.Report((int)((double)totalBytesRead / content.Length * 100));
            }
            
            await call.RequestStream.CompleteAsync();
            var response = await call;
            
            return response.Success;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to upload file: {ex.Message}", ex);
        }
    }

    public async Task<(bool Success, byte[] Content)> DownloadFileAsync(string fileId, IProgress<int>? progress = null)
    {
        if (!IsLoggedIn)
            throw new InvalidOperationException("Not logged in");

        try
        {
            var request = new FileDownloadRequest
            {
                Token = _token!,
                FileId = fileId
            };
            
            using var call = _client.DownloadFile(request);
            using var memoryStream = new MemoryStream();
            
            FileMetadata? metadata = null;
            long totalBytesReceived = 0;
            
            while (await call.ResponseStream.MoveNext(CancellationToken.None))
            {
                var response = call.ResponseStream.Current;
                
                if (response.DataCase == FileDownloadResponse.DataOneofCase.Metadata)
                {
                    metadata = response.Metadata;
                }
                else if (response.DataCase == FileDownloadResponse.DataOneofCase.ChunkData)
                {
                    await memoryStream.WriteAsync(response.ChunkData.ToByteArray());
                    
                    if (metadata != null)
                    {
                        totalBytesReceived += response.ChunkData.Length;
                        progress?.Report((int)((double)totalBytesReceived / metadata.TotalSize * 100));
                    }
                }
            }
            
            return (true, memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to download file: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteFileAsync(string fileId)
    {
        if (!IsLoggedIn)
            throw new InvalidOperationException("Not logged in");

        try
        {
            var request = new DeleteFileRequest
            {
                Token = _token!,
                FileId = fileId
            };
            
            var response = await _client.DeleteFileAsync(request);
            return response.Success;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete file: {ex.Message}", ex);
        }
    }

    public async Task<(bool Success, byte[] Content)> PreviewFileAsync(string fileId, IProgress<int>? progress = null)
    {
        return await DownloadFileAsync(fileId, progress);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        GC.SuppressFinalize(this);
    }
}