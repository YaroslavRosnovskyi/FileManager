using FileManager.Core.Models;

namespace FileManager.Core.Interfaces;

public interface IFileService
{
    Task<IEnumerable<FileItem>> ListFilesAsync(string token, string folderPath, string? fileTypeFilter = null, string? sortBy = null, bool ascending = true);
    Task<(bool Success, string FileId, string ErrorMessage)> UploadFileAsync(string token, string fileName, string folderPath, Stream fileStream);
    Task<(bool Success, Stream FileStream, string ErrorMessage)> DownloadFileAsync(string token, string fileId);
    Task<(bool Success, string ErrorMessage)> DeleteFileAsync(string token, string fileId);
    Task<(bool Success, string ErrorMessage)> SynchronizeFolderAsync(string token, string localFolderPath, string remoteFolderPath, IProgress<(int FilesProcessed, int FilesSynced, int TotalFiles)>? progress = null);
}