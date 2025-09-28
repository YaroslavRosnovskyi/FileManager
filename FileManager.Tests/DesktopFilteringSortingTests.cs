using FileManager.Core.Models;
using System.Collections.ObjectModel;

namespace FileManager.Tests;

[TestClass]
public class DesktopFilteringSortingTests
{
    private List<FileItem> _testFiles = null!;

    [TestInitialize]
    public void Setup()
    {
        _testFiles = new List<FileItem>
        {
            new FileItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Application.cs",
                Path = "/src",
                Size = 2048,
                FileType = ".cs",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                ModifiedAt = DateTime.UtcNow.AddDays(-1),
                CreatedBy = "Developer",
                ModifiedBy = "Tester"
            },
            new FileItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = "algorithm.cpp",
                Path = "/src/cpp",
                Size = 4096,
                FileType = ".cpp",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = "CppDev",
                ModifiedBy = "CppDev"
            },
            new FileItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = "header.hpp",
                Path = "/include",
                Size = 1024,
                FileType = ".hpp",
                CreatedAt = DateTime.UtcNow.AddDays(-4),
                ModifiedAt = DateTime.UtcNow.AddDays(-2),
                CreatedBy = "CppDev",
                ModifiedBy = "Developer"
            },
            new FileItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = "screenshot.png",
                Path = "/images",
                Size = 8192,
                FileType = ".png",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                ModifiedAt = DateTime.UtcNow.AddHours(-2),
                CreatedBy = "Designer",
                ModifiedBy = "Designer"
            },
            new FileItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = "photo.jpg",
                Path = "/images",
                Size = 6144,
                FileType = ".jpg",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                ModifiedAt = DateTime.UtcNow.AddDays(-3),
                CreatedBy = "Photographer",
                ModifiedBy = "Editor"
            }
        };
    }

    [TestMethod]
    public void DesktopFileTypeFilters_ContainsExpectedFilters()
    {
        // Arrange
        var filters = new ObservableCollection<string>
        {
            "All Files", 
            ".cpp",
            ".png"
        };

        // Act & Assert
        Assert.IsTrue(filters.Contains("All Files"));
        Assert.IsTrue(filters.Contains(".cpp"));
        Assert.IsTrue(filters.Contains(".png"));
        Assert.AreEqual(3, filters.Count, "Should only contain All Files, .cpp, and .png");
    }

    [TestMethod]
    public void DesktopFilterFilesByCpp_ReturnsOnlyCppFiles()
    {
        // Arrange
        string filterType = ".cpp";

        // Act
        var filteredFiles = _testFiles.Where(f => f.FileType == filterType).ToList();

        // Assert
        Assert.AreEqual(1, filteredFiles.Count);
        Assert.AreEqual("algorithm.cpp", filteredFiles[0].Name);
        Assert.AreEqual(".cpp", filteredFiles[0].FileType);
    }

    [TestMethod]
    public void DesktopFilterFilesByPng_ReturnsOnlyPngFiles()
    {
        // Arrange
        string filterType = ".png";

        // Act
        var filteredFiles = _testFiles.Where(f => f.FileType == filterType).ToList();

        // Assert
        Assert.AreEqual(1, filteredFiles.Count);
        Assert.AreEqual("screenshot.png", filteredFiles[0].Name);
        Assert.AreEqual(".png", filteredFiles[0].FileType);
    }

    [TestMethod]
    public void DesktopFilterFilesByAllFiles_ReturnsAllFiles()
    {
        // Arrange
        string filterType = "All Files";

        // Act
        var filteredFiles = filterType == "All Files" ? _testFiles : _testFiles.Where(f => f.FileType == filterType).ToList();

        // Assert
        Assert.AreEqual(5, filteredFiles.Count);
    }

    [TestMethod]
    public void DesktopSortFilesByName_Ascending_ReturnsSortedFiles()
    {
        // Arrange
        var files = new List<FileItem>(_testFiles);

        // Act
        var sortedFiles = files.OrderBy(f => f.Name).ToList();

        // Assert
        Assert.AreEqual("algorithm.cpp", sortedFiles[0].Name);
        Assert.AreEqual("Application.cs", sortedFiles[1].Name);
        Assert.AreEqual("header.hpp", sortedFiles[2].Name);
        Assert.AreEqual("photo.jpg", sortedFiles[3].Name);
        Assert.AreEqual("screenshot.png", sortedFiles[4].Name);
    }

    [TestMethod]
    public void DesktopSortFilesByName_Descending_ReturnsSortedFiles()
    {
        // Arrange
        var files = new List<FileItem>(_testFiles);

        // Act
        var sortedFiles = files.OrderByDescending(f => f.Name).ToList();

        // Assert
        Assert.AreEqual("screenshot.png", sortedFiles[0].Name);
        Assert.AreEqual("photo.jpg", sortedFiles[1].Name);
        Assert.AreEqual("header.hpp", sortedFiles[2].Name);
        Assert.AreEqual("Application.cs", sortedFiles[3].Name);
        Assert.AreEqual("algorithm.cpp", sortedFiles[4].Name);
    }

    [TestMethod]
    public void DesktopSortFilesBySize_Ascending_ReturnsSortedFiles()
    {
        // Arrange
        var files = new List<FileItem>(_testFiles);

        // Act
        var sortedFiles = files.OrderBy(f => f.Size).ToList();

        // Assert
        Assert.AreEqual(1024, sortedFiles[0].Size);
        Assert.AreEqual(2048, sortedFiles[1].Size);
        Assert.AreEqual(4096, sortedFiles[2].Size);
        Assert.AreEqual(6144, sortedFiles[3].Size);
        Assert.AreEqual(8192, sortedFiles[4].Size);
    }

    [TestMethod]
    public void DesktopSortFilesByCreatedAt_Ascending_ReturnsSortedFiles()
    {
        // Arrange
        var files = new List<FileItem>(_testFiles);

        // Act
        var sortedFiles = files.OrderBy(f => f.CreatedAt).ToList();

        // Assert
        Assert.AreEqual("photo.jpg", sortedFiles[0].Name);
        Assert.AreEqual("screenshot.png", sortedFiles[4].Name);
    }

    [TestMethod]
    public void DesktopSortFilesByCreatedBy_Ascending_ReturnsSortedFiles()
    {
        // Arrange
        var files = new List<FileItem>(_testFiles);

        // Act
        var sortedFiles = files.OrderBy(f => f.CreatedBy).ToList();

        // Assert
        Assert.AreEqual("CppDev", sortedFiles[0].CreatedBy);
        Assert.AreEqual("CppDev", sortedFiles[1].CreatedBy);
        Assert.AreEqual("Designer", sortedFiles[2].CreatedBy);
        Assert.AreEqual("Developer", sortedFiles[3].CreatedBy);
        Assert.AreEqual("Photographer", sortedFiles[4].CreatedBy);
    }

    [TestMethod]
    public void DesktopCombinedFilterAndSort_CppAndHppFilesOrderedBySize_ReturnsCorrectResult()
    {
        // Arrange
        var cppRelatedFiles = _testFiles.Where(f => f.FileType == ".cpp" || f.FileType == ".hpp").ToList();

        // Act
        var result = cppRelatedFiles.OrderBy(f => f.Size).ToList();

        // Assert
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("header.hpp", result[0].Name);
        Assert.AreEqual("algorithm.cpp", result[1].Name);
    }

    [TestMethod]
    public void DesktopFilterImageFiles_PngAndJpg_ReturnsImageFiles()
    {
        // Arrange
        var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };

        // Act
        var imageFiles = _testFiles.Where(f => imageExtensions.Contains(f.FileType)).ToList();

        // Assert
        Assert.AreEqual(2, imageFiles.Count);
        Assert.IsTrue(imageFiles.Any(f => f.Name == "screenshot.png"));
        Assert.IsTrue(imageFiles.Any(f => f.Name == "photo.jpg"));
    }

    [TestMethod]
    public void DesktopSortOptions_ContainsExpectedOptions()
    {
        // Arrange
        var sortOptions = new ObservableCollection<string>
        {
            "Name",
            "Size",
            "Type",
            "Created At",
            "Modified At",
            "Created By",
            "Modified By"
        };

        // Act & Assert
        Assert.IsTrue(sortOptions.Contains("Name"));
        Assert.IsTrue(sortOptions.Contains("Size"));
        Assert.IsTrue(sortOptions.Contains("Type"));
        Assert.IsTrue(sortOptions.Contains("Created At"));
        Assert.IsTrue(sortOptions.Contains("Modified At"));
        Assert.IsTrue(sortOptions.Contains("Created By"));
        Assert.IsTrue(sortOptions.Contains("Modified By"));
        Assert.AreEqual(7, sortOptions.Count);
    }

    [TestMethod]
    public void DesktopObservableCollectionBehavior_AddingFiles_TriggersNotification()
    {
        // Arrange
        var files = new ObservableCollection<FileItem>();
        bool notificationTriggered = false;
        
        files.CollectionChanged += (sender, e) => {
            notificationTriggered = true;
        };

        // Act
        files.Add(_testFiles[0]);

        // Assert
        Assert.IsTrue(notificationTriggered);
        Assert.AreEqual(1, files.Count);
    }
}