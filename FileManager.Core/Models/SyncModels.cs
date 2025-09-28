namespace FileManager.Core.Models;

public enum SyncStatus
{
    Syncing,
    Completed,
    Failed
}

public class SyncProgress
{
    public SyncStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public int FilesProcessed { get; set; }
    public int FilesSynced { get; set; }
    public int TotalFiles { get; set; }
}

public class SyncResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public int FilesProcessed { get; set; }
    public int FilesSynced { get; set; }
    public int TotalFiles { get; set; }
}