using FileManager.Web.Services;

namespace FileManager.Tests;

[TestClass]
public class FileUtilsTests
{
    [TestMethod]
    public void FormatFileSize_WithSmallSize_ReturnsBytes()
    {
        // Arrange
        long sizeInBytes = 512;

        // Act
        string result = FileUtils.FormatFileSize(sizeInBytes);

        // Assert
        Assert.AreEqual("512 B", result);
    }

    [TestMethod]
    public void FormatFileSize_WithKilobytes_ReturnsKB()
    {
        // Arrange
        long sizeInBytes = 1536; // 1.5 KB

        // Act
        string result = FileUtils.FormatFileSize(sizeInBytes);

        // Assert
        Assert.AreEqual("1.5 KB", result);
    }

    [TestMethod]
    public void FormatFileSize_WithMegabytes_ReturnsMB()
    {
        // Arrange
        long sizeInBytes = 2097152; // 2 MB

        // Act
        string result = FileUtils.FormatFileSize(sizeInBytes);

        // Assert
        Assert.AreEqual("2 MB", result);
    }

    [TestMethod]
    public void GetFileIcon_WithCppFile_ReturnsCodeIcon()
    {
        // Arrange
        string fileType = ".cpp";

        // Act
        string result = FileUtils.GetFileIcon(fileType);

        // Assert
        Assert.AreEqual("bi-file-code", result);
    }

    [TestMethod]
    public void GetFileIcon_WithPngFile_ReturnsImageIcon()
    {
        // Arrange
        string fileType = ".png";

        // Act
        string result = FileUtils.GetFileIcon(fileType);

        // Assert
        Assert.AreEqual("bi-file-image", result);
    }

    [TestMethod]
    public void GetFileIcon_WithUnknownFile_ReturnsDefaultIcon()
    {
        // Arrange
        string fileType = ".xyz";

        // Act
        string result = FileUtils.GetFileIcon(fileType);

        // Assert
        Assert.AreEqual("bi-file", result);
    }

    [TestMethod]
    public void IsImageFile_WithPngFile_ReturnsTrue()
    {
        // Arrange
        string fileType = ".png";

        // Act
        bool result = FileUtils.IsImageFile(fileType);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsImageFile_WithCppFile_ReturnsFalse()
    {
        // Arrange
        string fileType = ".cpp";

        // Act
        bool result = FileUtils.IsImageFile(fileType);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsTextFile_WithCppFile_ReturnsTrue()
    {
        // Arrange
        string fileType = ".cpp";

        // Act
        bool result = FileUtils.IsTextFile(fileType);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsTextFile_WithHeaderFile_ReturnsTrue()
    {
        // Arrange
        string fileType = ".hpp";

        // Act
        bool result = FileUtils.IsTextFile(fileType);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsTextFile_WithPngFile_ReturnsFalse()
    {
        // Arrange
        string fileType = ".png";

        // Act
        bool result = FileUtils.IsTextFile(fileType);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void GetContentType_WithCppFile_ReturnsCorrectMimeType()
    {
        // Arrange
        string fileType = ".cpp";

        // Act
        string result = FileUtils.GetContentType(fileType);

        // Assert
        Assert.AreEqual("text/x-c++src", result);
    }

    [TestMethod]
    public void GetContentType_WithPngFile_ReturnsCorrectMimeType()
    {
        // Arrange
        string fileType = ".png";

        // Act
        string result = FileUtils.GetContentType(fileType);

        // Assert
        Assert.AreEqual("image/png", result);
    }

    [TestMethod]
    public void GetContentType_WithUnknownFile_ReturnsOctetStream()
    {
        // Arrange
        string fileType = ".unknown";

        // Act
        string result = FileUtils.GetContentType(fileType);

        // Assert
        Assert.AreEqual("application/octet-stream", result);
    }
}