using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileManager.Core.Models;
using FileManager.Desktop.Services;
using FileManager.Proto;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace FileManager.Desktop.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly FileService.FileServiceClient _client;
    private readonly GrpcChannel _channel;
    private string? _token;
    private LocalFolderWatcher? _folderWatcher;
    private string? _watchedFolderPath;

    private bool _isLoggedIn;
    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set => SetProperty(ref _isLoggedIn, value);
    }

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private string _statusMessage = "Please log in";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private int _progressValue;
    public int ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    private bool _isOperationInProgress;
    public bool IsOperationInProgress
    {
        get => _isOperationInProgress;
        set => SetProperty(ref _isOperationInProgress, value);
    }

    private FileItem? _selectedFile;
    public FileItem? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    public ObservableCollection<FileItem> Files { get; } = new();

    public ObservableCollection<string> FileTypeFilters { get; } = new()
    {
        "All Files",
        ".cpp",
        ".png"
    };

    private string _selectedFileTypeFilter = "All Files";
    public string SelectedFileTypeFilter
    {
        get => _selectedFileTypeFilter;
        set
        {
            if (SetProperty(ref _selectedFileTypeFilter, value))
            {
                RefreshFilesAsync().ConfigureAwait(false);
            }
        }
    }

    public ObservableCollection<string> SortOptions { get; } = new()
    {
        "Name",
        "Size",
        "Type",
        "Created At",
        "Modified At",
        "Created By",
        "Modified By"
    };

    private string _selectedSortOption = "Name";
    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                RefreshFilesAsync().ConfigureAwait(false);
            }
        }
    }

    private bool _sortAscending = true;
    public bool SortAscending
    {
        get => _sortAscending;
        set
        {
            if (SetProperty(ref _sortAscending, value))
            {
                RefreshFilesAsync().ConfigureAwait(false);
            }
        }
    }

    private bool _showSizeColumn = true;
    public bool ShowSizeColumn
    {
        get => _showSizeColumn;
        set
        {
            if (SetProperty(ref _showSizeColumn, value))
            {
                StatusMessage = $"Size column: {(value ? "Shown" : "Hidden")}";
            }
        }
    }

    private bool _showTypeColumn = true;
    public bool ShowTypeColumn
    {
        get => _showTypeColumn;
        set
        {
            if (SetProperty(ref _showTypeColumn, value))
            {
                StatusMessage = $"Type column: {(value ? "Shown" : "Hidden")}";
            }
        }
    }

    private bool _showCreatedAtColumn = true;
    public bool ShowCreatedAtColumn
    {
        get => _showCreatedAtColumn;
        set
        {
            if (SetProperty(ref _showCreatedAtColumn, value))
            {
                StatusMessage = $"Created At column: {(value ? "Shown" : "Hidden")}";
            }
        }
    }

    private bool _showModifiedAtColumn = true;
    public bool ShowModifiedAtColumn
    {
        get => _showModifiedAtColumn;
        set
        {
            if (SetProperty(ref _showModifiedAtColumn, value))
            {
                StatusMessage = $"Modified At column: {(value ? "Shown" : "Hidden")}";
            }
        }
    }

    private bool _showCreatedByColumn = true;
    public bool ShowCreatedByColumn
    {
        get => _showCreatedByColumn;
        set
        {
            if (SetProperty(ref _showCreatedByColumn, value))
            {
                StatusMessage = $"Created By column: {(value ? "Shown" : "Hidden")}";
            }
        }
    }

    private bool _showModifiedByColumn = true;
    public bool ShowModifiedByColumn
    {
        get => _showModifiedByColumn;
        set
        {
            if (SetProperty(ref _showModifiedByColumn, value))
            {
                StatusMessage = $"Modified By column: {(value ? "Shown" : "Hidden")}";
            }
        }
    }

    public IRelayCommand LoginCommand { get; }
    public IRelayCommand LogoutCommand { get; }
    public IRelayCommand UploadCommand { get; }
    public IRelayCommand DownloadCommand { get; }
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand SyncFolderCommand { get; }
    public IRelayCommand PreviewCommand { get; }
    public IRelayCommand DoubleClickCommand { get; }

    public IRelayCommand ToggleSizeColumnCommand { get; }
    public IRelayCommand ToggleTypeColumnCommand { get; }
    public IRelayCommand ToggleCreatedAtColumnCommand { get; }
    public IRelayCommand ToggleModifiedAtColumnCommand { get; }
    public IRelayCommand ToggleCreatedByColumnCommand { get; }
    public IRelayCommand ToggleModifiedByColumnCommand { get; }

    public IRelayCommand TestColumnCommand { get; }

    public MainViewModel()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        string serverUrl = configuration["ServerConnection:GrpcUrl"] ?? "https://localhost:7121";

        PropertyChanged += OnPropertyChanged;

        var httpHandler = new HttpClientHandler();
        httpHandler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        _channel = GrpcChannel.ForAddress(serverUrl, new GrpcChannelOptions
        {
            HttpHandler = httpHandler,
            HttpVersion = new Version(2, 0)
        });
        _client = new FileService.FileServiceClient(_channel);

        LoginCommand = new AsyncRelayCommand(LoginAsync);
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);
        UploadCommand = new AsyncRelayCommand(UploadFileAsync, () => IsLoggedIn && !IsOperationInProgress);
        DownloadCommand = new AsyncRelayCommand(DownloadFileAsync, () => IsLoggedIn && SelectedFile != null && !IsOperationInProgress);
        DeleteCommand = new AsyncRelayCommand(DeleteFileAsync, () => IsLoggedIn && SelectedFile != null && !IsOperationInProgress);
        SyncFolderCommand = new AsyncRelayCommand(SyncFolderAsync, () => IsLoggedIn && !IsOperationInProgress);
        PreviewCommand = new AsyncRelayCommand(PreviewFileAsync, () => IsLoggedIn && SelectedFile != null && !IsOperationInProgress);
        DoubleClickCommand = new AsyncRelayCommand<FileItem>(async (file) =>
        {
            SelectedFile = file;
            await PreviewFileAsync();
        }, (file) => IsLoggedIn && file != null && !IsOperationInProgress);

        ToggleSizeColumnCommand = new RelayCommand(() => ShowSizeColumn = !ShowSizeColumn);
        ToggleTypeColumnCommand = new RelayCommand(() => ShowTypeColumn = !ShowTypeColumn);
        ToggleCreatedAtColumnCommand = new RelayCommand(() => ShowCreatedAtColumn = !ShowCreatedAtColumn);
        ToggleModifiedAtColumnCommand = new RelayCommand(() => ShowModifiedAtColumn = !ShowModifiedAtColumn);
        ToggleCreatedByColumnCommand = new RelayCommand(() => ShowCreatedByColumn = !ShowCreatedByColumn);
        ToggleModifiedByColumnCommand = new RelayCommand(() => ShowModifiedByColumn = !ShowModifiedByColumn);

        TestColumnCommand = new RelayCommand(() =>
        {
            StatusMessage = $"Test: Size={ShowSizeColumn}, Type={ShowTypeColumn}, CreatedAt={ShowCreatedAtColumn}, ModifiedAt={ShowModifiedAtColumn}, CreatedBy={ShowCreatedByColumn}, ModifiedBy={ShowModifiedByColumn}";
        });
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsLoggedIn) || e.PropertyName == nameof(IsOperationInProgress) || e.PropertyName == nameof(SelectedFile))
        {
            (UploadCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (DownloadCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (DeleteCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (SyncFolderCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (PreviewCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }
    }

    private async Task LoginAsync()
    {
        try
        {
            IsOperationInProgress = true;
            StatusMessage = "Logging in...";

            var request = new LoginRequest
            {
                Username = Username,
                Password = Password
            };

            var response = await _client.LoginAsync(request);

            if (response.Success)
            {
                _token = response.Token;
                IsLoggedIn = true;
                StatusMessage = "Logged in successfully";

                await RefreshFilesAsync();
            }
            else
            {
                StatusMessage = $"Login failed: {response.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
        }
    }

    private async Task LogoutAsync()
    {
        try
        {
            IsOperationInProgress = true;
            StatusMessage = "Logging out...";

            if (string.IsNullOrEmpty(_token))
            {
                IsLoggedIn = false;
                return;
            }

            var request = new LogoutRequest
            {
                Token = _token
            };

            var response = await _client.LogoutAsync(request);

            if (response.Success)
            {
                _token = null;
                IsLoggedIn = false;
                Files.Clear();
                StatusMessage = "Logged out successfully";
            }
            else
            {
                StatusMessage = "Logout failed";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
        }
    }

    private async Task RefreshFilesAsync()
    {
        if (!IsLoggedIn || string.IsNullOrEmpty(_token))
            return;

        try
        {
            IsOperationInProgress = true;
            StatusMessage = "Loading files...";

            string fileTypeFilter = SelectedFileTypeFilter == "All Files" ? string.Empty : SelectedFileTypeFilter;
            string sortBy = SelectedSortOption.Replace(" ", "").ToLower();

            var request = new ListFilesRequest
            {
                Token = _token,
                FolderPath = "/",
                FileTypeFilter = fileTypeFilter,
                SortBy = sortBy,
                Ascending = SortAscending
            };

            var response = await _client.ListFilesAsync(request);

            Files.Clear();
            foreach (var fileInfo in response.Files)
            {
                Files.Add(new FileItem
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

            StatusMessage = $"Loaded {Files.Count} files";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading files: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
        }
    }

    private async Task UploadFileAsync()
    {
        if (!IsLoggedIn || string.IsNullOrEmpty(_token))
            return;

        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select File to Upload",
            Filter = "All Files (*.*)|*.*|C++ Files (*.cpp)|*.cpp|PNG Files (*.png)|*.png",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() != true)
            return;

        try
        {
            IsOperationInProgress = true;
            string filePath = openFileDialog.FileName;
            string fileName = Path.GetFileName(filePath);

            StatusMessage = $"Checking for conflicts: {fileName}...";

            using var fileStream = File.OpenRead(filePath);

            var conflictRequest = new CheckFileConflictRequest
            {
                Token = _token,
                FileName = fileName,
                FolderPath = "/",
                FileSize = fileStream.Length
            };

            var conflictResponse = await _client.CheckFileConflictAsync(conflictRequest);

            bool overwriteExisting = false;
            string? overwriteFileId = null;
            string finalFileName = fileName;

            if (conflictResponse.HasConflict)
            {
                var conflictResult = ShowConflictDialog(fileName, conflictResponse.ConflictingFiles);

                if (conflictResult.Action == ConflictAction.Cancel)
                {
                    StatusMessage = "Upload cancelled";
                    return;
                }

                if (conflictResult.Action == ConflictAction.Overwrite && !string.IsNullOrEmpty(conflictResult.OverwriteFileId))
                {
                    overwriteExisting = true;
                    overwriteFileId = conflictResult.OverwriteFileId;
                }
                else if (conflictResult.Action == ConflictAction.KeepBoth)
                {
                    finalFileName = GenerateUniqueFileName(fileName);
                }
            }

            StatusMessage = $"Uploading {finalFileName}...";

            using var call = _client.UploadFile();

            await call.RequestStream.WriteAsync(new FileUploadRequest
            {
                Metadata = new FileMetadata
                {
                    Token = _token,
                    FileName = finalFileName,
                    FolderPath = "/",
                    TotalSize = fileStream.Length
                },
                OverwriteExisting = overwriteExisting,
                OverwriteFileId = overwriteFileId ?? string.Empty
            });

            byte[] buffer = new byte[64 * 1024];
            int bytesRead;
            long totalBytesRead = 0;

            fileStream.Position = 0;
            while ((bytesRead = await fileStream.ReadAsync(buffer)) > 0)
            {
                await call.RequestStream.WriteAsync(new FileUploadRequest
                {
                    ChunkData = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead)
                });

                totalBytesRead += bytesRead;
                ProgressValue = (int)((double)totalBytesRead / fileStream.Length * 100);
            }

            await call.RequestStream.CompleteAsync();

            var response = await call;

            if (response.Success)
            {
                StatusMessage = $"File uploaded successfully. ID: {response.FileId}";
                await RefreshFilesAsync();
            }
            else
            {
                StatusMessage = $"Upload failed: {response.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error uploading file: {ex.Message}";
        }
        finally
        {
            ProgressValue = 0;
            IsOperationInProgress = false;
        }
    }

    private FileConflictResult ShowConflictDialog(string fileName, IEnumerable<ConflictingFile> conflictingFiles)
    {
        var conflictingFilesList = conflictingFiles.ToList();

        var dialogBuilder = new System.Text.StringBuilder();
        dialogBuilder.AppendLine($"A file with the name '{fileName}' already exists!");
        dialogBuilder.AppendLine();

        if (conflictingFilesList.Count == 1)
        {
            var existing = conflictingFilesList[0];
            dialogBuilder.AppendLine("Existing file:");
            dialogBuilder.AppendLine($"  Name: {existing.Name}");
            dialogBuilder.AppendLine($"  Size: {FormatFileSize(existing.Size)}");
            dialogBuilder.AppendLine($"  Modified: {DateTime.Parse(existing.ModifiedAt):yyyy-MM-dd HH:mm:ss}");
            dialogBuilder.AppendLine($"  Modified By: {existing.ModifiedBy}");
            dialogBuilder.AppendLine();
            dialogBuilder.AppendLine("What would you like to do?");
            dialogBuilder.AppendLine();
            dialogBuilder.AppendLine("Yes = Replace existing file");
            dialogBuilder.AppendLine("No = Keep both files (rename new file)");
            dialogBuilder.AppendLine("Cancel = Cancel upload");

            var result = System.Windows.MessageBox.Show(
                dialogBuilder.ToString(),
                "File Conflict",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            return result switch
            {
                MessageBoxResult.Yes => new FileConflictResult { Action = ConflictAction.Overwrite, OverwriteFileId = existing.Id },
                MessageBoxResult.No => new FileConflictResult { Action = ConflictAction.KeepBoth },
                _ => new FileConflictResult { Action = ConflictAction.Cancel }
            };
        }
        else
        {
            dialogBuilder.AppendLine("Multiple files with the same name exist:");
            for (int i = 0; i < conflictingFilesList.Count; i++)
            {
                var existing = conflictingFilesList[i];
                dialogBuilder.AppendLine($"{i + 1}. {existing.Name} ({FormatFileSize(existing.Size)}, modified {DateTime.Parse(existing.ModifiedAt):yyyy-MM-dd HH:mm:ss})");
            }
            dialogBuilder.AppendLine();
            dialogBuilder.AppendLine("Choose an option:");
            dialogBuilder.AppendLine("OK = Keep both files (rename new file)");
            dialogBuilder.AppendLine("Cancel = Cancel upload");
            dialogBuilder.AppendLine();
            dialogBuilder.AppendLine("Note: To replace a specific file, cancel and try again with a different approach.");

            var result = System.Windows.MessageBox.Show(
                dialogBuilder.ToString(),
                "File Conflict - Multiple Files",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            return result switch
            {
                MessageBoxResult.OK => new FileConflictResult { Action = ConflictAction.KeepBoth },
                _ => new FileConflictResult { Action = ConflictAction.Cancel }
            };
        }
    }

    private string GenerateUniqueFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
        var counter = 1;
        string newFileName;

        do
        {
            newFileName = $"{nameWithoutExtension} ({counter}){extension}";
            counter++;
        } while (counter < 100 && Files.Any(f => f.Name.Equals(newFileName, StringComparison.OrdinalIgnoreCase)));

        return newFileName;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private async Task DownloadFileAsync()
    {
        if (!IsLoggedIn || string.IsNullOrEmpty(_token) || SelectedFile == null)
            return;

        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save File",
            FileName = SelectedFile.Name,
            Filter = "All Files (*.*)|*.*"
        };

        if (saveFileDialog.ShowDialog() != true)
            return;

        try
        {
            IsOperationInProgress = true;
            StatusMessage = $"Downloading {SelectedFile.Name}...";

            var request = new FileDownloadRequest
            {
                Token = _token,
                FileId = SelectedFile.Id
            };

            using var call = _client.DownloadFile(request);
            using var fileStream = File.Create(saveFileDialog.FileName);

            FileMetadata? metadata = null;
            long totalBytesReceived = 0;

            while (await call.ResponseStream.MoveNext())
            {
                var response = call.ResponseStream.Current;

                if (response.DataCase == FileDownloadResponse.DataOneofCase.Metadata)
                {
                    metadata = response.Metadata;
                }
                else if (response.DataCase == FileDownloadResponse.DataOneofCase.ChunkData)
                {
                    await fileStream.WriteAsync(response.ChunkData.ToByteArray());

                    if (metadata != null)
                    {
                        totalBytesReceived += response.ChunkData.Length;
                        ProgressValue = (int)((double)totalBytesReceived / metadata.TotalSize * 100);
                    }
                }
            }

            StatusMessage = $"File downloaded successfully to {saveFileDialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error downloading file: {ex.Message}";
        }
        finally
        {
            ProgressValue = 0;
            IsOperationInProgress = false;
        }
    }

    private async Task DeleteFileAsync()
    {
        if (!IsLoggedIn || string.IsNullOrEmpty(_token) || SelectedFile == null)
            return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete '{SelectedFile.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsOperationInProgress = true;
            StatusMessage = $"Deleting {SelectedFile.Name}...";

            var request = new DeleteFileRequest
            {
                Token = _token,
                FileId = SelectedFile.Id
            };

            var response = await _client.DeleteFileAsync(request);

            if (response.Success)
            {
                StatusMessage = $"File '{SelectedFile.Name}' deleted successfully";
                await RefreshFilesAsync();
            }
            else
            {
                StatusMessage = $"Delete failed: {response.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting file: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
        }
    }

    private async Task SyncFolderAsync()
    {
        if (!IsLoggedIn || string.IsNullOrEmpty(_token))
            return;

        var folderDialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a folder to synchronize",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        try
        {
            IsOperationInProgress = true;
            StatusMessage = "Starting folder synchronization...";
            ProgressValue = 0;

            await PerformClientDrivenSync(folderDialog.SelectedPath, "/");

            StartWatching(folderDialog.SelectedPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error during synchronization: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"An error occurred during synchronization: {ex.Message}",
                "Synchronization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        finally
        {
            ProgressValue = 0;
            IsOperationInProgress = false;
        }
    }

    private async Task PerformClientDrivenSync(string localFolderPath, string remoteFolderPath)
    {
        try
        {
            StatusMessage = "Getting server file list...";

            var serverFilesRequest = new GetSyncFileListRequest
            {
                Token = _token!,
                RemoteFolderPath = remoteFolderPath
            };

            var serverFilesResponse = await _client.GetSyncFileListAsync(serverFilesRequest);
            var serverFiles = serverFilesResponse.Files.ToList();

            StatusMessage = $"Found {serverFiles.Count} files on server";

            var localFiles = Directory.GetFiles(localFolderPath, "*.*", SearchOption.AllDirectories)
                .Select(f => new System.IO.FileInfo(f))
                .ToList();

            StatusMessage = $"Found {localFiles.Count} local files";

            int totalOperations = localFiles.Count + serverFiles.Count;
            int currentOperation = 0;
            int syncedCount = 0;

            StatusMessage = "Uploading local files to server...";

            foreach (var localFile in localFiles)
            {
                currentOperation++;
                ProgressValue = (int)((double)currentOperation / totalOperations * 50);

                string relativePath = Path.GetRelativePath(localFolderPath, localFile.DirectoryName ?? string.Empty);
                string remoteRelativePath = Path.Combine(remoteFolderPath, relativePath).Replace('\\', '/');
                if (remoteRelativePath.EndsWith("/") && remoteRelativePath.Length > 1)
                {
                    remoteRelativePath = remoteRelativePath[..^1];
                }

                var serverFile = serverFiles.FirstOrDefault(sf =>
                    sf.Name == localFile.Name &&
                    sf.Path == remoteRelativePath);

                bool shouldUpload = false;

                if (serverFile == null)
                {
                    shouldUpload = true;
                }
                else
                {
                    DateTime serverModifiedAt = DateTime.Parse(serverFile.ModifiedAt);
                    if (localFile.LastWriteTimeUtc > serverModifiedAt)
                    {
                        shouldUpload = true;
                    }
                }

                if (shouldUpload)
                {
                    StatusMessage = $"Uploading: {localFile.Name}";

                    try
                    {
                        using var fileStream = localFile.OpenRead();
                        using var call = _client.SyncUploadFile();

                        await call.RequestStream.WriteAsync(new SyncFileUploadRequest
                        {
                            Metadata = new SyncFileMetadata
                            {
                                Token = _token!,
                                FileName = localFile.Name,
                                FolderPath = remoteRelativePath,
                                TotalSize = fileStream.Length,
                                LocalModifiedAt = localFile.LastWriteTimeUtc.ToString("o")
                            }
                        });

                        byte[] buffer = new byte[64 * 1024];
                        int bytesRead;

                        while ((bytesRead = await fileStream.ReadAsync(buffer)) > 0)
                        {
                            await call.RequestStream.WriteAsync(new SyncFileUploadRequest
                            {
                                ChunkData = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead)
                            });
                        }

                        await call.RequestStream.CompleteAsync();
                        var response = await call;

                        if (response.Success)
                        {
                            syncedCount++;
                            StatusMessage = $"Uploaded: {localFile.Name}";
                        }
                        else
                        {
                            StatusMessage = $"Upload failed: {localFile.Name} - {response.ErrorMessage}";
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Error uploading {localFile.Name}: {ex.Message}";
                    }
                }
                else
                {
                    StatusMessage = $"Skipped: {localFile.Name} (up to date)";
                }
            }

            StatusMessage = "Downloading server files...";

            foreach (var serverFile in serverFiles)
            {
                currentOperation++;
                ProgressValue = 50 + (int)((double)(currentOperation - localFiles.Count) / serverFiles.Count * 50);

                string localRelativePath = serverFile.Path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                string localFilePath;

                if (string.IsNullOrEmpty(localRelativePath) || localRelativePath == ".")
                {
                    localFilePath = Path.Combine(localFolderPath, serverFile.Name);
                }
                else
                {
                    localFilePath = Path.Combine(localFolderPath, localRelativePath, serverFile.Name);
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
                    var localFileInfo = new System.IO.FileInfo(localFilePath);
                    DateTime serverModifiedAt = DateTime.Parse(serverFile.ModifiedAt);
                    if (serverModifiedAt > localFileInfo.LastWriteTimeUtc)
                    {
                        shouldDownload = true;
                    }
                }

                if (shouldDownload)
                {
                    StatusMessage = $"Downloading: {serverFile.Name}";

                    try
                    {
                        var downloadRequest = new FileDownloadRequest
                        {
                            Token = _token!,
                            FileId = serverFile.FileId
                        };

                        using var call = _client.DownloadFile(downloadRequest);
                        using var fileStream = File.Create(localFilePath);

                        while (await call.ResponseStream.MoveNext())
                        {
                            var response = call.ResponseStream.Current;

                            if (response.DataCase == FileDownloadResponse.DataOneofCase.ChunkData)
                            {
                                await fileStream.WriteAsync(response.ChunkData.ToByteArray());
                            }
                        }

                        DateTime serverModifiedAt = DateTime.Parse(serverFile.ModifiedAt);
                        File.SetLastWriteTimeUtc(localFilePath, serverModifiedAt);

                        syncedCount++;
                        StatusMessage = $"Downloaded: {serverFile.Name}";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Error downloading {serverFile.Name}: {ex.Message}";
                    }
                }
                else
                {
                    StatusMessage = $"Skipped: {serverFile.Name} (up to date)";
                }
            }

            ProgressValue = 100;
            StatusMessage = $"Synchronization completed! Synced {syncedCount} files.";

            System.Windows.MessageBox.Show(
                $"Successfully synchronized {syncedCount} files between the server and {localFolderPath}",
                "Synchronization Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            await RefreshFilesAsync();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
        {
            StatusMessage = "Using legacy sync method...";
            await PerformLegacySync(localFolderPath, remoteFolderPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync failed: {ex.Message}";
            throw;
        }
    }

    private void StartWatching(string localFolderPath)
    {
        if (_watchedFolderPath == localFolderPath) return;

        _folderWatcher?.Dispose();
        _watchedFolderPath = localFolderPath;

        _folderWatcher = new LocalFolderWatcher(localFolderPath,
            msg => System.Diagnostics.Debug.WriteLine($"[watcher] {msg}"));

        _folderWatcher.FolderChanged += async (_, _) =>
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                await PerformClientDrivenSync(localFolderPath, "/"));
    }

    private async Task PerformLegacySync(string localFolderPath, string remoteFolderPath)
    {
        var request = new SyncFolderRequest
        {
            Token = _token!,
            LocalFolderPath = localFolderPath,
            RemoteFolderPath = remoteFolderPath
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMinutes(30));

        using var call = _client.SynchronizeFolder(request, cancellationToken: cts.Token);

        int filesSynced = 0;

        try
        {
            while (await call.ResponseStream.MoveNext(cts.Token))
            {
                var response = call.ResponseStream.Current;

                if (response.Status == SyncFolderResponse.Types.SyncStatus.Syncing)
                {
                    string progressDetail = "";

                    if (response.Message.Contains("Phase 1:"))
                    {
                        progressDetail = "Phase 1: Uploading local files to server";
                    }
                    else if (response.Message.Contains("Phase 2:"))
                    {
                        progressDetail = "Phase 2: Downloading server files to local folder";
                    }
                    else if (response.Message.Contains("Phase 3:"))
                    {
                        progressDetail = "Phase 3: Cleaning up deleted files from local folder";
                    }
                    else if (response.Message.Contains("Uploaded:"))
                    {
                        progressDetail = "Uploading files to server";
                    }
                    else if (response.Message.Contains("Downloaded:"))
                    {
                        progressDetail = "Downloading files from server";
                    }
                    else if (response.Message.Contains("Deleted local file:"))
                    {
                        progressDetail = "Removing files deleted from server";
                    }
                    else
                    {
                        progressDetail = "Synchronizing files";
                    }

                    StatusMessage = $"{progressDetail}: {response.Message}";
                    filesSynced = response.FilesSynced;

                    ProgressValue = response.TotalFiles > 0
                        ? (int)((double)response.FilesProcessed / response.TotalFiles * 100)
                        : 0;
                }
                else if (response.Status == SyncFolderResponse.Types.SyncStatus.Completed)
                {
                    StatusMessage = $"Synchronization completed. Synced {response.FilesSynced} files.";
                    filesSynced = response.FilesSynced;
                    ProgressValue = 100;

                    await RefreshFilesAsync();
                    break;
                }
                else if (response.Status == SyncFolderResponse.Types.SyncStatus.Failed)
                {
                    StatusMessage = $"Synchronization failed: {response.Message}";
                    break;
                }
            }

            if (filesSynced > 0)
            {
                System.Windows.MessageBox.Show(
                    $"Successfully synchronized {filesSynced} files",
                    "Synchronization Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            StatusMessage = "Synchronization was cancelled";
            System.Windows.MessageBox.Show(
                "The synchronization operation was cancelled",
                "Synchronization Cancelled",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Synchronization was cancelled due to timeout";
            System.Windows.MessageBox.Show(
                "The synchronization operation timed out",
                "Synchronization Timeout",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private async Task PreviewFileAsync()
    {
        if (!IsLoggedIn || string.IsNullOrEmpty(_token) || SelectedFile == null)
            return;

        try
        {
            IsOperationInProgress = true;
            StatusMessage = $"Loading preview for {SelectedFile.Name}...";

            var request = new FileDownloadRequest
            {
                Token = _token,
                FileId = SelectedFile.Id
            };

            using var call = _client.DownloadFile(request);
            using var memoryStream = new MemoryStream();

            FileMetadata? metadata = null;

            while (await call.ResponseStream.MoveNext())
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
                        ProgressValue = (int)((double)memoryStream.Length / metadata.TotalSize * 100);
                    }
                }
            }

            memoryStream.Position = 0;

            var previewWindow = new FilePreviewWindow(SelectedFile, memoryStream.ToArray());
            previewWindow.Owner = System.Windows.Application.Current.MainWindow;
            previewWindow.ShowDialog();

            StatusMessage = $"Preview loaded for {SelectedFile.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading file preview: {ex.Message}";
        }
        finally
        {
            ProgressValue = 0;
            IsOperationInProgress = false;
        }
    }

    public async void HandleFileDrop(string[] files)
    {
        if (!IsLoggedIn || string.IsNullOrEmpty(_token) || IsOperationInProgress)
            return;

        try
        {
            IsOperationInProgress = true;

            foreach (var filePath in files)
            {
                if (File.Exists(filePath))
                {
                    string fileName = Path.GetFileName(filePath);

                    StatusMessage = $"Checking for conflicts: {fileName}...";

                    using var fileStream = File.OpenRead(filePath);

                    var conflictRequest = new CheckFileConflictRequest
                    {
                        Token = _token,
                        FileName = fileName,
                        FolderPath = "/",
                        FileSize = fileStream.Length
                    };

                    var conflictResponse = await _client.CheckFileConflictAsync(conflictRequest);

                    bool overwriteExisting = false;
                    string? overwriteFileId = null;
                    string finalFileName = fileName;

                    if (conflictResponse.HasConflict)
                    {
                        var conflictResult = ShowConflictDialog(fileName, conflictResponse.ConflictingFiles);

                        if (conflictResult.Action == ConflictAction.Cancel)
                        {
                            StatusMessage = $"Upload of {fileName} cancelled";
                            continue;
                        }

                        if (conflictResult.Action == ConflictAction.Overwrite && !string.IsNullOrEmpty(conflictResult.OverwriteFileId))
                        {
                            overwriteExisting = true;
                            overwriteFileId = conflictResult.OverwriteFileId;
                        }
                        else if (conflictResult.Action == ConflictAction.KeepBoth)
                        {
                            finalFileName = GenerateUniqueFileName(fileName);
                        }
                    }

                    StatusMessage = $"Uploading {finalFileName}...";

                    using var call = _client.UploadFile();

                    await call.RequestStream.WriteAsync(new FileUploadRequest
                    {
                        Metadata = new FileMetadata
                        {
                            Token = _token,
                            FileName = finalFileName,
                            FolderPath = "/",
                            TotalSize = fileStream.Length
                        },
                        OverwriteExisting = overwriteExisting,
                        OverwriteFileId = overwriteFileId ?? string.Empty
                    });

                    byte[] buffer = new byte[64 * 1024];
                    int bytesRead;
                    long totalBytesRead = 0;

                    fileStream.Position = 0;
                    while ((bytesRead = await fileStream.ReadAsync(buffer)) > 0)
                    {
                        await call.RequestStream.WriteAsync(new FileUploadRequest
                        {
                            ChunkData = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead)
                        });

                        totalBytesRead += bytesRead;
                        ProgressValue = (int)((double)totalBytesRead / fileStream.Length * 100);
                    }

                    await call.RequestStream.CompleteAsync();

                    var response = await call;

                    if (!response.Success)
                    {
                        StatusMessage = $"Upload failed: {response.ErrorMessage}";
                        break;
                    }
                }
            }

            StatusMessage = "Files uploaded successfully";
            await RefreshFilesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error uploading files: {ex.Message}";
        }
        finally
        {
            ProgressValue = 0;
            IsOperationInProgress = false;
        }
    }


    private class FileConflictResult
    {
        public ConflictAction Action { get; set; }
        public string? OverwriteFileId { get; set; }
    }

    private enum ConflictAction
    {
        Cancel,
        Overwrite,
        KeepBoth
    }


    public string? GetCurrentToken()
    {
        return _token;
    }

    public FileService.FileServiceClient GetGrpcClient()
    {
        return _client;
    }
}