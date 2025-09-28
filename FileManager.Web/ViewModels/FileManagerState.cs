using FileManager.Core.Models;

namespace FileManager.Web.ViewModels;

public class FileManagerState
{
    public bool IsLoggedIn { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool IsOperationInProgress { get; set; }
    public string StatusMessage { get; set; } = "Please log in";
    public int ProgressValue { get; set; }

    public List<FileItem> Files { get; set; } = new();
    public FileItem? SelectedFile { get; set; }

    public string SelectedFileTypeFilter { get; set; } = "All Files";
    public string SelectedSortOption { get; set; } = "name";
    public bool SortAscending { get; set; } = true;

    public Dictionary<string, bool> VisibleColumns { get; set; } = new()
    {
        ["Size"] = true,
        ["Type"] = true,
        ["CreatedAt"] = true,
        ["ModifiedAt"] = true,
        ["CreatedBy"] = true,
        ["ModifiedBy"] = true,
        ["Actions"] = true
    };

    public bool ShowPreview { get; set; }
    public FileItem? PreviewFile { get; set; }
    public string? PreviewDataUrl { get; set; }
    public string? PreviewText { get; set; }
    public string PreviewActiveTab { get; set; } = "content";

    public bool ShowInfoModal { get; set; }

    public List<string> FileTypeFilters { get; } = new()
    {
        "All Files", ".cpp", ".png"
    };

    public List<string> SortOptions { get; } = new()
    {
        "name", "createdat", "modifiedat", "createdby", "modifiedby", "size"
    };

    public void Reset()
    {
        IsLoggedIn = false;
        Username = string.Empty;
        Password = string.Empty;
        IsOperationInProgress = false;
        StatusMessage = "Please log in";
        ProgressValue = 0;
        Files.Clear();
        SelectedFile = null;
        ShowPreview = false;
        PreviewFile = null;
        PreviewDataUrl = null;
        PreviewText = null;
        ShowInfoModal = false;
    }
}