using OneClick.Domain.Operations;
using Shouldly;
using Xunit;

namespace OneClick.Domain.Tests.Operations;

public class FileOperationTests
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var type = OperationType.Rename;
        var source = @"C:\test\file.txt";
        var destination = @"C:\test\newfile.txt";
        var size = 1024L;
        var modified = DateTime.UtcNow;

        // Act
        var operation = new FileOperation(type, source, destination, size, modified);

        // Assert
        operation.Type.ShouldBe(type);
        operation.Source.ShouldBe(source);
        operation.Destination.ShouldBe(destination);
        operation.Size.ShouldBe(size);
        operation.ModifiedUtc.ShouldBe(modified);
    }

    [Fact]
    public void SourceFileName_ShouldReturnFileNameOnly()
    {
        // Arrange
        var operation = FileOperation.Move(@"C:\folder\subfolder\test.txt", @"C:\other\dest.txt", 100, DateTime.UtcNow);

        // Act & Assert
        operation.SourceFileName.ShouldBe("test.txt");
    }

    [Fact]
    public void DestinationFileName_ShouldReturnFileNameOnly()
    {
        // Arrange
        var operation = FileOperation.Move(@"C:\folder\test.txt", @"C:\other\subfolder\dest.txt", 100, DateTime.UtcNow);

        // Act & Assert
        operation.DestinationFileName.ShouldBe("dest.txt");
    }

    [Theory]
    [InlineData(@"C:\test\file.txt", @"C:\test\newfile.txt", false)] // Same drive
    [InlineData(@"C:\test\file.txt", @"D:\newfile.txt", true)]       // Different drive
    [InlineData(@"C:\test\file.txt", @"\\server\share\file.txt", true)] // UNC path
    public void IsCrossDrive_ShouldDetectCorrectly(string source, string destination, bool expected)
    {
        // Arrange
        var operation = FileOperation.Move(source, destination, 100, DateTime.UtcNow);

        // Act & Assert
        operation.IsCrossDrive.ShouldBe(expected);
    }

    [Fact]
    public void Move_ShouldCreateMoveOperation()
    {
        // Arrange
        var source = @"C:\test\file.txt";
        var destination = @"C:\other\file.txt";
        var size = 500L;
        var modified = DateTime.UtcNow;

        // Act
        var operation = FileOperation.Move(source, destination, size, modified);

        // Assert
        operation.Type.ShouldBe(OperationType.Move);
        operation.Source.ShouldBe(source);
        operation.Destination.ShouldBe(destination);
    }

    [Fact]
    public void Rename_ShouldCreateRenameOperation()
    {
        // Arrange
        var source = @"C:\test\file.txt";
        var destination = @"C:\test\renamed.txt";
        var size = 500L;
        var modified = DateTime.UtcNow;

        // Act
        var operation = FileOperation.Rename(source, destination, size, modified);

        // Assert
        operation.Type.ShouldBe(OperationType.Rename);
        operation.Source.ShouldBe(source);
        operation.Destination.ShouldBe(destination);
    }

    [Fact]
    public void Hold_ShouldCreateHoldOperation()
    {
        // Arrange
        var source = @"C:\test\file.txt";
        var destination = @"C:\OneClick\holding\file.txt";
        var size = 500L;
        var modified = DateTime.UtcNow;

        // Act
        var operation = FileOperation.Hold(source, destination, size, modified);

        // Assert
        operation.Type.ShouldBe(OperationType.Hold);
        operation.Source.ShouldBe(source);
        operation.Destination.ShouldBe(destination);
    }
}