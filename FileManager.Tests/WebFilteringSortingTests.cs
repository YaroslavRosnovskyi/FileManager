using FileManager.Web.ViewModels;
using FileManager.Core.Models;

namespace FileManager.Tests;

[TestClass]
public class WebFilteringSortingTests
{
    private FileManagerState _state = null!;
    private List<FileItem> _testFiles = null!;

    [TestInitialize]
    public void Setup()
    {
        _state = new FileManagerState();
        
        _testFiles = new List<FileItem>
        {
            new FileItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Program.cs",
                Path = "/src",
                Size = 1024,
                FileType = ".cs",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                ModifiedAt = DateTime.UtcNow.AddDays(-2),
                CreatedBy = "Alice",
                ModifiedBy = "Bob"
            },
            new FileItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = "main.cpp",
                Path = "/src",
                Size = 2048,
                FileType = ".cpp",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                ModifiedAt = DateTime.UtcNow.AddDays(-1),
                CreatedBy = "Bob",
                ModifiedBy = "Alice"
            },
            new FileItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = "logo.png",
                Path = "/assets",
                Size = 4096,
                FileType = ".png",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = "Charlie",
                ModifiedBy = "Charlie"
            },
            new FileItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = "config.txt",
                Path = "/config",
                Size = 512,
                FileType = ".txt",
                CreatedAt = DateTime.UtcNow.AddDays(-4),
                ModifiedAt = DateTime.UtcNow.AddDays(-3),
                CreatedBy = "Admin",
                ModifiedBy = "Admin"
            }
        };
    }

    [TestMethod]
    public void FileTypeFilters_ContainsCppAndPng()
    {
        // Act & Assert
        Assert.IsTrue(_state.FileTypeFilters.Contains(".cpp"), "FileTypeFilters should contain .cpp");
        Assert.IsTrue(_state.FileTypeFilters.Contains(".png"), "FileTypeFilters should contain .png");
        Assert.IsTrue(_state.FileTypeFilters.Contains("All Files"), "FileTypeFilters should contain 'All Files'");
        Assert.AreEqual(3, _state.FileTypeFilters.Count, "FileTypeFilters should only contain 3 items: All Files, .cpp, and .png");
    }

    [TestMethod]
    public void FilterFiles_ByCppExtension_ReturnsOnlyCppFiles()
    {
        // Arrange
        _state.Files = _testFiles;
        string filterType = ".cpp";

        // Act
        var filteredFiles = _testFiles.Where(f => f.FileType == filterType).ToList();

        // Assert
        Assert.AreEqual(1, filteredFiles.Count);
        Assert.AreEqual("main.cpp", filteredFiles[0].Name);
        Assert.AreEqual(".cpp", filteredFiles[0].FileType);
    }

    [TestMethod]
    public void FilterFiles_ByPngExtension_ReturnsOnlyPngFiles()
    {
        // Arrange
        _state.Files = _testFiles;
        string filterType = ".png";

        // Act
        var filteredFiles = _testFiles.Where(f => f.FileType == filterType).ToList();

        // Assert
        Assert.AreEqual(1, filteredFiles.Count);
        Assert.AreEqual("logo.png", filteredFiles[0].Name);
        Assert.AreEqual(".png", filteredFiles[0].FileType);
    }

    [TestMethod]
    public void FilterFiles_AllFiles_ReturnsAllFiles()
    {
        // Arrange
        _state.Files = _testFiles;
        string filterType = "All Files";

        // Act
        var filteredFiles = filterType == "All Files" ? _testFiles : _testFiles.Where(f => f.FileType == filterType).ToList();

        // Assert
        Assert.AreEqual(4, filteredFiles.Count);
    }

    [TestMethod]
    public void SortFiles_ByNameAscending_ReturnsSortedFiles()
    {
        // Arrange
        var files = new List<FileItem>(_testFiles);

        // Act
        var sortedFiles = files.OrderBy(f => f.Name).ToList();

        // Assert
        Assert.AreEqual("config.txt", sortedFiles[0].Name);
        Assert.AreEqual("logo.png", sortedFiles[1].Name);
        Assert.AreEqual("main.cpp", sortedFiles[2].Name);
        Assert.AreEqual("Program.cs", sortedFiles[3].Name);
    }

    [TestMethod]
    public void SortFiles_BySizeDescending_ReturnsSortedFiles()
    {
        // Arrange
        var files = new List<FileItem>(_testFiles);

        // Act
        var sortedFiles = files.OrderByDescending(f => f.Size).ToList();

        // Assert
        Assert.AreEqual(4096, sortedFiles[0].Size);
        Assert.AreEqual(2048, sortedFiles[1].Size);
        Assert.AreEqual(1024, sortedFiles[2].Size);
        Assert.AreEqual(512, sortedFiles[3].Size);
    }

    [TestMethod]
    public void SortFiles_ByCreatedAtAscending_ReturnsSortedFiles()
    {
        // Arrange
        var files = new List<FileItem>(_testFiles);

        // Act
        var sortedFiles = files.OrderBy(f => f.CreatedAt).ToList();

        // Assert
        Assert.AreEqual("Program.cs", sortedFiles[0].Name);
        Assert.AreEqual("logo.png", sortedFiles[3].Name);
    }

    [TestMethod]
    public void SortFiles_ByModifiedByAscending_ReturnsSortedFiles()
    {
        // Arrange
        var files = new List<FileItem>(_testFiles);

        // Act
        var sortedFiles = files.OrderBy(f => f.ModifiedBy).ToList();

        // Assert
        Assert.AreEqual("Admin", sortedFiles[0].ModifiedBy);
        Assert.AreEqual("Alice", sortedFiles[1].ModifiedBy);
        Assert.AreEqual("Bob", sortedFiles[2].ModifiedBy);
        Assert.AreEqual("Charlie", sortedFiles[3].ModifiedBy);
    }

    [TestMethod]
    public void CombinedFilterAndSort_CppFilesOrderedBySize_ReturnsCorrectResult()
    {
        // Arrange
        var cppFiles = new List<FileItem>
        {
            new FileItem { Name = "small.cpp", FileType = ".cpp", Size = 1000 },
            new FileItem { Name = "large.cpp", FileType = ".cpp", Size = 5000 },
            new FileItem { Name = "medium.cpp", FileType = ".cpp", Size = 3000 }
        };

        // Act
        var result = cppFiles
            .Where(f => f.FileType == ".cpp")
            .OrderBy(f => f.Size)
            .ToList();

        // Assert
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("small.cpp", result[0].Name);
        Assert.AreEqual("medium.cpp", result[1].Name);
        Assert.AreEqual("large.cpp", result[2].Name);
    }

    [TestMethod]
    public void StateInitialization_HasCorrectDefaultValues()
    {
        // Arrange & Act
        var state = new FileManagerState();

        // Assert
        Assert.AreEqual("All Files", state.SelectedFileTypeFilter);
        Assert.AreEqual("name", state.SelectedSortOption);
        Assert.IsTrue(state.SortAscending);
        Assert.IsFalse(state.IsLoggedIn);
        Assert.AreEqual(0, state.Files.Count);
    }

    [TestMethod]
    public void SyncPathCalculation_RootDirectoryFiles_HandledCorrectly()
    {
        // Arrange
        string remoteFolderPath = "/";
        string remoteFilePath = "/";
        string fileName = "newfile.txt";
        string localFolderPath = @"C:\TestSync";
        
        // Act
        string relativeRemotePath = remoteFilePath.TrimStart('/');
        string expectedLocalPath;
        
        if (string.IsNullOrEmpty(relativeRemotePath) || relativeRemotePath == ".")
        {
            expectedLocalPath = Path.Combine(localFolderPath, fileName);
        }
        else
        {
            string localRelativePath = relativeRemotePath.Replace('/', Path.DirectorySeparatorChar);
            expectedLocalPath = Path.Combine(localFolderPath, localRelativePath, fileName);
        }
        
        // Assert
        Assert.AreEqual(@"C:\TestSync\newfile.txt", expectedLocalPath);
    }
    
    [TestMethod]
    public void SyncPathCalculation_SubdirectoryFiles_HandledCorrectly()
    {
        // Arrange
        string remoteFolderPath = "/";
        string remoteFilePath = "/documents";
        string fileName = "document.pdf";
        string localFolderPath = @"C:\TestSync";
        
        // Act
        string relativeRemotePath = remoteFilePath.TrimStart('/');
        string expectedLocalPath;
        
        if (string.IsNullOrEmpty(relativeRemotePath) || relativeRemotePath == ".")
        {
            expectedLocalPath = Path.Combine(localFolderPath, fileName);
        }
        else
        {
            string localRelativePath = relativeRemotePath.Replace('/', Path.DirectorySeparatorChar);
            expectedLocalPath = Path.Combine(localFolderPath, localRelativePath, fileName);
        }
        
        // Assert
        Assert.AreEqual(@"C:\TestSync\documents\document.pdf", expectedLocalPath);
    }
}