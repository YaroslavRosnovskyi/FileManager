using FileManager.Core.Models;

namespace FileManager.Core.Interfaces;

public interface IStorageService
{
    Task<IEnumerable<FileItem>> GetFilesAsync(string token, string folderPath, string? fileTypeFilter = null, string? sortBy = null, bool ascending = true);
    Task<(string FileId, long FileSize)> SaveFileAsync(string token, string fileName, string folderPath, Stream fileStream);
    Task<(Stream FileStream, FileItem Metadata)> GetFileAsync(string token, string fileId);
    Task<bool> DeleteFileAsync(string token, string fileId);
    Task<SyncResult> SynchronizeFolderAsync(string token, string localFolderPath, string remoteFolderPath, IProgress<SyncProgress>? progress = null);
}