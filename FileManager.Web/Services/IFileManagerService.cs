using FileManager.Core.Models;
using FileManager.Proto;

namespace FileManager.Web.Services;

public interface IFileManagerService
{
    Task<bool> LoginAsync(string username, string password);
    Task<bool> LogoutAsync();
    Task<List<FileItem>> GetFilesAsync(string folderPath = "/", string fileTypeFilter = "", string sortBy = "name", bool ascending = true);
    Task<(bool HasConflict, List<FileItem> ConflictingFiles)> CheckFileConflictAsync(string fileName, long fileSize, string folderPath = "/");
    Task<bool> UploadFileAsync(string fileName, byte[] content, string folderPath = "/", IProgress<int>? progress = null, bool overwriteExisting = false, string? overwriteFileId = null);
    Task<(bool Success, byte[] Content)> DownloadFileAsync(string fileId, IProgress<int>? progress = null);
    Task<bool> DeleteFileAsync(string fileId);
    Task<(bool Success, byte[] Content)> PreviewFileAsync(string fileId, IProgress<int>? progress = null);
    
    bool IsLoggedIn { get; }
    string? CurrentToken { get; }
}