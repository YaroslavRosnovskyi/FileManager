using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FileManager.Desktop.ViewModels;
using FileManager.Desktop.Converters;
using FileManager.Core.Models;
using System.IO;

namespace FileManager.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private FileItem? _draggedFile;
    private bool _isDragging;
        
    public MainWindow()
    {
        InitializeComponent();
            
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
            
        PasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
            
        Loaded += (s, e) => {
            FilesDataGrid.Focus();
            SetupColumnVisibilityBindings();
            SetupDragOutFunctionality();
        };
    }
    
    private void SetupColumnVisibilityBindings()
    {
        var converter = new BoolToVisibilityConverter();
        
        if (FilesDataGrid.Columns.Count > 1)
        {
            var sizeBinding = new System.Windows.Data.Binding("ShowSizeColumn")
            {
                Source = _viewModel,
                Converter = converter
            };
            BindingOperations.SetBinding(FilesDataGrid.Columns[1], DataGridColumn.VisibilityProperty, sizeBinding);
            
            var typeBinding = new System.Windows.Data.Binding("ShowTypeColumn")
            {
                Source = _viewModel,
                Converter = converter
            };
            BindingOperations.SetBinding(FilesDataGrid.Columns[2], DataGridColumn.VisibilityProperty, typeBinding);
            
            var createdAtBinding = new System.Windows.Data.Binding("ShowCreatedAtColumn")
            {
                Source = _viewModel,
                Converter = converter
            };
            BindingOperations.SetBinding(FilesDataGrid.Columns[3], DataGridColumn.VisibilityProperty, createdAtBinding);
            
            var modifiedAtBinding = new System.Windows.Data.Binding("ShowModifiedAtColumn")
            {
                Source = _viewModel,
                Converter = converter
            };
            BindingOperations.SetBinding(FilesDataGrid.Columns[4], DataGridColumn.VisibilityProperty, modifiedAtBinding);
            
            var createdByBinding = new System.Windows.Data.Binding("ShowCreatedByColumn")
            {
                Source = _viewModel,
                Converter = converter
            };
            BindingOperations.SetBinding(FilesDataGrid.Columns[5], DataGridColumn.VisibilityProperty, createdByBinding);
            
            var modifiedByBinding = new System.Windows.Data.Binding("ShowModifiedByColumn")
            {
                Source = _viewModel,
                Converter = converter
            };
            BindingOperations.SetBinding(FilesDataGrid.Columns[6], DataGridColumn.VisibilityProperty, modifiedByBinding);
        }
    }

    private void SetupDragOutFunctionality()
    {
        FilesDataGrid.PreviewMouseLeftButtonDown += FilesDataGrid_PreviewMouseLeftButtonDown;
        FilesDataGrid.MouseMove += FilesDataGrid_MouseMove;
        FilesDataGrid.MouseLeftButtonUp += FilesDataGrid_MouseLeftButtonUp;
        FilesDataGrid.QueryContinueDrag += FilesDataGrid_QueryContinueDrag;
        FilesDataGrid.GiveFeedback += FilesDataGrid_GiveFeedback;
    }
        
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Password = PasswordBox.Password;
        }
    }
        
    private void FileList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                _viewModel.HandleFileDrop(files);
            }
        }
    }

    #region Drag-Out Functionality

    private System.Windows.Point _startPoint;
    private bool _isDragReady;

    private void FilesDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(null);
        _isDragReady = true;
        _isDragging = false;
        
        var row = GetDataGridRowFromPoint(e.GetPosition(FilesDataGrid));
        if (row != null)
        {
            _draggedFile = row.Item as FileItem;
        }
    }

    private void FilesDataGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragReady || _isDragging || e.LeftButton != MouseButtonState.Pressed)
            return;

        System.Windows.Point mousePos = e.GetPosition(null);
        Vector diff = _startPoint - mousePos;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            if (_draggedFile != null)
            {
                StartFileDragOut(_draggedFile);
            }
        }
    }

    private void FilesDataGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragReady = false;
        _isDragging = false;
        _draggedFile = null;
    }

    private async void StartFileDragOut(FileItem file)
    {
        if (_isDragging || !_viewModel.IsLoggedIn)
            return;

        _isDragging = true;
        
        try
        {
            _viewModel.StatusMessage = $"Preparing {file.Name} for drag-out download...";
            
            var tempFile = await DownloadFileToTemp(file);
            if (tempFile == null)
            {
                _viewModel.StatusMessage = "Failed to prepare file for drag-out";
                return;
            }

            var dataObject = new System.Windows.DataObject();
            
            var filePaths = new string[] { tempFile };
            dataObject.SetData(System.Windows.DataFormats.FileDrop, filePaths);
            
            dataObject.SetData(System.Windows.DataFormats.Text, file.Name);
            
            dataObject.SetData("FileManager.FileItem", file);

            _viewModel.StatusMessage = $"Drag {file.Name} to any location to download it";

            var result = System.Windows.DragDrop.DoDragDrop(FilesDataGrid, dataObject, System.Windows.DragDropEffects.Copy);
            
            CleanupTempFile(tempFile, file.Name);

            _viewModel.StatusMessage = result == System.Windows.DragDropEffects.Copy ? 
                $"Successfully downloaded {file.Name}" : 
                $"Drag operation cancelled for {file.Name}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Error during drag-out: {ex.Message}";
        }
        finally
        {
            _isDragging = false;
        }
    }

    private async void CleanupTempFile(string tempFilePath, string originalFileName)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3000);
                
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                    System.Diagnostics.Debug.WriteLine($"Cleaned up temporary file for: {originalFileName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not delete temp file for {originalFileName}: {ex.Message}");
                
                try
                {
                    await Task.Delay(10000);
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                        System.Diagnostics.Debug.WriteLine($"Successfully cleaned up temp file on retry for: {originalFileName}");
                    }
                }
                catch (Exception retryEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Final cleanup attempt failed for {originalFileName}: {retryEx.Message}");
                }
            }
        });
    }

    private async Task<string?> DownloadFileToTemp(FileItem file)
    {
        try
        {
            var tempPath = System.IO.Path.GetTempPath();
            var sanitizedFileName = SanitizeFileName(file.Name);
            var tempFileName = System.IO.Path.Combine(tempPath, sanitizedFileName);
            
            if (File.Exists(tempFileName))
            {
                var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(sanitizedFileName);
                var extension = System.IO.Path.GetExtension(sanitizedFileName);
                var counter = 1;
                
                do
                {
                    tempFileName = System.IO.Path.Combine(tempPath, $"{fileNameWithoutExt} ({counter}){extension}");
                    counter++;
                } while (File.Exists(tempFileName));
            }
            
            var request = new FileManager.Proto.FileDownloadRequest
            {
                Token = _viewModel.GetCurrentToken(),
                FileId = file.Id
            };
            
            using var call = _viewModel.GetGrpcClient().DownloadFile(request);
            using var fileStream = File.Create(tempFileName);
            
            FileManager.Proto.FileMetadata? metadata = null;
            
            while (await call.ResponseStream.MoveNext(CancellationToken.None))
            {
                var response = call.ResponseStream.Current;
                
                if (response.DataCase == FileManager.Proto.FileDownloadResponse.DataOneofCase.Metadata)
                {
                    metadata = response.Metadata;
                }
                else if (response.DataCase == FileManager.Proto.FileDownloadResponse.DataOneofCase.ChunkData)
                {
                    await fileStream.WriteAsync(response.ChunkData.ToByteArray());
                }
            }
            
            return tempFileName;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error downloading file to temp: {ex.Message}");
            return null;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        var sanitized = fileName;
        
        foreach (var invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }
        
        sanitized = sanitized.Replace(':', '_')
                           .Replace('*', '_')
                           .Replace('?', '_')
                           .Replace('"', '_')
                           .Replace('<', '_')
                           .Replace('>', '_')
                           .Replace('|', '_');
        
        if (sanitized.Length > 200)
        {
            var extension = System.IO.Path.GetExtension(sanitized);
            var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(sanitized);
            sanitized = nameWithoutExt.Substring(0, 200 - extension.Length) + extension;
        }
        
        return sanitized;
    }

    private void FilesDataGrid_QueryContinueDrag(object sender, System.Windows.QueryContinueDragEventArgs e)
    {
        if (e.EscapePressed)
        {
            e.Action = System.Windows.DragAction.Cancel;
        }
        else if ((e.KeyStates & DragDropKeyStates.LeftMouseButton) == 0)
        {
            e.Action = System.Windows.DragAction.Drop;
        }
        else
        {
            e.Action = System.Windows.DragAction.Continue;
        }
    }

    private void FilesDataGrid_GiveFeedback(object sender, System.Windows.GiveFeedbackEventArgs e)
    {
        if (e.Effects == System.Windows.DragDropEffects.Copy)
        {
            System.Windows.Input.Mouse.SetCursor(System.Windows.Input.Cursors.Hand);
            e.UseDefaultCursors = false;
        }
        else
        {
            e.UseDefaultCursors = true;
        }
        e.Handled = true;
    }

    private DataGridRow? GetDataGridRowFromPoint(System.Windows.Point point)
    {
        var hitTest = VisualTreeHelper.HitTest(FilesDataGrid, point);
        if (hitTest?.VisualHit != null)
        {
            var element = hitTest.VisualHit;
            while (element != null && element is not DataGridRow)
            {
                element = VisualTreeHelper.GetParent(element);
            }
            return element as DataGridRow;
        }
        return null;
    }

    #endregion
}