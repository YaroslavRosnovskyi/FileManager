namespace FileManager.Web.Services;

public static class FileUtils
{
    public static string FormatFileSize(long sizeInBytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = sizeInBytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
    
    public static string GetFileIcon(string fileType)
    {
        return fileType.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "bi-file-image",
            ".pdf" => "bi-file-pdf",
            ".doc" or ".docx" => "bi-file-word",
            ".xls" or ".xlsx" => "bi-file-excel",
            ".ppt" or ".pptx" => "bi-file-ppt",
            ".txt" => "bi-file-text",
            ".zip" or ".rar" or ".7z" => "bi-file-zip",
            ".cs" or ".js" or ".html" or ".css" or ".xml" or ".json" or ".cpp" or ".h" or ".hpp" => "bi-file-code",
            _ => "bi-file"
        };
    }
    
    public static bool IsImageFile(string fileType)
    {
        return fileType.ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp";
    }
    
    public static bool IsTextFile(string fileType)
    {
        return fileType.ToLowerInvariant() is ".txt" or ".cs" or ".js" or ".html" or ".css" or ".xml" or ".json" or ".md" or ".cpp" or ".h" or ".hpp";
    }

    public static string GetContentType(string fileType)
    {
        return fileType.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "text/javascript",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".cpp" => "text/x-c++src",
            ".h" => "text/x-chdr",
            ".hpp" => "text/x-c++hdr",
            ".doc" or ".docx" => "application/msword",
            ".xls" or ".xlsx" => "application/vnd.ms-excel",
            ".ppt" or ".pptx" => "application/vnd.ms-powerpoint",
            _ => "application/octet-stream"
        };
    }

    public static string GetMimeType(string fileType)
    {
        return fileType.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }
}