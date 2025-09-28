using FileManager.Core.Models;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using FileManager.Desktop.Converters;

namespace FileManager.Desktop
{
    public partial class FilePreviewWindow : Window
    {
        private readonly FileItem _file;
        private readonly byte[] _fileContent;
        private readonly FileSizeConverter _fileSizeConverter = new();

        public FilePreviewWindow(FileItem file, byte[] fileContent)
        {
            InitializeComponent();
            _file = file;
            _fileContent = fileContent;
            
            LoadFileInfo();
            LoadFileContent();
        }
        
        private void LoadFileInfo()
        {
            Title = $"Preview: {_file.Name}";
            
            FileNameText.Text = _file.Name;
            FileTypeText.Text = _file.FileType;
            FileSizeText.Text = _fileSizeConverter.Convert(_file.Size, typeof(string), null!, null!).ToString();
            FileModifiedText.Text = _file.ModifiedAt.ToString("g");
            
            PropFileName.Text = _file.Name;
            PropFilePath.Text = _file.Path;
            PropFileType.Text = _file.FileType;
            PropFileSize.Text = _fileSizeConverter.Convert(_file.Size, typeof(string), null!, null!).ToString();
            PropCreatedAt.Text = _file.CreatedAt.ToString("g");
            PropModifiedAt.Text = _file.ModifiedAt.ToString("g");
            PropCreatedBy.Text = _file.CreatedBy;
            PropModifiedBy.Text = _file.ModifiedBy;
        }
        
        private void LoadFileContent()
        {
            try
            {
                string fileExtension = _file.FileType.ToLowerInvariant();
                
                if (fileExtension == ".cs" || fileExtension == ".txt" || fileExtension == ".json" || 
                    fileExtension == ".xml" || fileExtension == ".md" || fileExtension == ".html" ||
                    fileExtension == ".css" || fileExtension == ".js" || fileExtension == ".cpp" ||
                    fileExtension == ".h" || fileExtension == ".hpp")
                {
                    TextContent.Visibility = Visibility.Visible;
                    ImageContent.Visibility = Visibility.Collapsed;
                    NoPreviewText.Visibility = Visibility.Collapsed;
                    
                    using (var memoryStream = new MemoryStream(_fileContent))
                    using (var streamReader = new StreamReader(memoryStream))
                    {
                        TextContent.Text = streamReader.ReadToEnd();
                    }
                }
                else if (fileExtension == ".jpg" || fileExtension == ".jpeg" || fileExtension == ".png" || 
                         fileExtension == ".gif" || fileExtension == ".bmp")
                {
                    TextContent.Visibility = Visibility.Collapsed;
                    ImageContent.Visibility = Visibility.Visible;
                    NoPreviewText.Visibility = Visibility.Collapsed;
                    
                    var bitmapImage = new BitmapImage();
                    using (var memoryStream = new MemoryStream(_fileContent))
                    {
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = memoryStream;
                        bitmapImage.EndInit();
                    }
                    
                    ImageContent.Source = bitmapImage;
                }
                else
                {
                    TextContent.Visibility = Visibility.Collapsed;
                    ImageContent.Visibility = Visibility.Collapsed;
                    NoPreviewText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                TextContent.Visibility = Visibility.Visible;
                ImageContent.Visibility = Visibility.Collapsed;
                NoPreviewText.Visibility = Visibility.Collapsed;
                
                TextContent.Text = $"Error loading file content: {ex.Message}\r\n\r\n{ex.StackTrace}";
            }
        }
    }
}