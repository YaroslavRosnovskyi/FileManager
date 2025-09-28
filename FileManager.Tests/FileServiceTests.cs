using FileManager.Core.Interfaces;
using FileManager.Proto;
using FileManager.Server.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Moq;

namespace FileManager.Tests;

[TestClass]
public class FileServiceTests
{
    private readonly Mock<ILogger<FileServiceImpl>> _loggerMock;
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<IStorageService> _storageServiceMock;
    private readonly FileServiceImpl _service;
    
    public FileServiceTests()
    {
        _loggerMock = new Mock<ILogger<FileServiceImpl>>();
        _authServiceMock = new Mock<IAuthService>();
        _storageServiceMock = new Mock<IStorageService>();
        _service = new FileServiceImpl(_loggerMock.Object, _authServiceMock.Object, _storageServiceMock.Object);
        
        _storageServiceMock.Setup(s => s.GetFilesAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<bool>()))
            .ReturnsAsync(new List<Core.Models.FileItem> 
            {
                new Core.Models.FileItem 
                { 
                    Id = Guid.NewGuid().ToString(),
                    Name = "Example.cs",
                    Path = "/",
                    Size = 1024,
                    FileType = ".cs",
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    ModifiedAt = DateTime.UtcNow.AddDays(-2),
                    CreatedBy = "Admin",
                    ModifiedBy = "User"
                },
                new Core.Models.FileItem 
                { 
                    Id = Guid.NewGuid().ToString(),
                    Name = "Example.jpg",
                    Path = "/",
                    Size = 2048,
                    FileType = ".jpg",
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    ModifiedAt = DateTime.UtcNow.AddDays(-1),
                    CreatedBy = "Admin",
                    ModifiedBy = "Admin"
                }
            });
            
        _storageServiceMock.Setup(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
            
        _authServiceMock.Setup(a => a.LogoutAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
    }
    
    [TestMethod]
    public async Task Login_WithValidCredentials_ReturnsSuccessfulResponse()
    {
        // Arrange
        _authServiceMock.Setup(a => a.LoginAsync("demo", "password"))
            .ReturnsAsync((true, "mock-token-guid", string.Empty));
            
        var request = new LoginRequest
        {
            Username = "demo",
            Password = "password"
        };
        
        var mockServerCallContext = new Mock<ServerCallContext>();
        
        // Act
        var response = await _service.Login(request, mockServerCallContext.Object);
        
        // Assert
        Assert.IsTrue(response.Success, $"Login should succeed but got error: {response.ErrorMessage}");
        Assert.IsFalse(string.IsNullOrEmpty(response.Token), "Token should not be empty on successful login");
        Assert.IsTrue(string.IsNullOrEmpty(response.ErrorMessage), "Error message should be empty on successful login");
    }
    
    [TestMethod]
    public async Task Login_WithInvalidCredentials_ReturnsFailureResponse()
    {
        // Arrange
        _authServiceMock.Setup(a => a.LoginAsync("demo", "wrongpassword"))
            .ReturnsAsync((false, string.Empty, "Invalid username or password"));
            
        var request = new LoginRequest
        {
            Username = "demo",
            Password = "wrongpassword"
        };
        
        var mockServerCallContext = new Mock<ServerCallContext>();
        
        // Act
        var response = await _service.Login(request, mockServerCallContext.Object);
        
        // Assert
        Assert.IsFalse(response.Success);
        Assert.IsTrue(string.IsNullOrEmpty(response.Token));
        Assert.IsFalse(string.IsNullOrEmpty(response.ErrorMessage));
    }
    
    [TestMethod]
    public async Task ListFiles_ReturnsFileList()
    {
        // Arrange
        var request = new ListFilesRequest
        {
            Token = "mock-token",
            FolderPath = "/"
        };
        
        var mockServerCallContext = new Mock<ServerCallContext>();
        
        // Act
        var response = await _service.ListFiles(request, mockServerCallContext.Object);
        
        // Assert
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Files.Count > 0);
        
        Assert.IsTrue(response.Files.Any(f => f.FileType == ".cs"));
        Assert.IsTrue(response.Files.Any(f => f.FileType == ".jpg"));
    }
    
    [TestMethod]
    public async Task DeleteFile_ReturnsSuccess()
    {
        // Arrange
        var request = new DeleteFileRequest
        {
            Token = "mock-token",
            FileId = "test-file-id"
        };
        
        var mockServerCallContext = new Mock<ServerCallContext>();
        
        // Act
        var response = await _service.DeleteFile(request, mockServerCallContext.Object);
        
        // Assert
        Assert.IsTrue(response.Success);
    }
}