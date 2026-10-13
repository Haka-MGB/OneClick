using OneClick.Domain.Operations;
using Shouldly;
using Xunit;

namespace OneClick.Domain.Tests.Operations;

public class OperationBatchTests
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var tool = "Test Tool";
        var label = "Test operation";
        var created = DateTime.UtcNow;
        var operations = new[]
        {
            FileOperation.Rename(@"C:\test\file1.txt", @"C:\test\newfile1.txt", 100, DateTime.UtcNow),
            FileOperation.Move(@"C:\test\file2.txt", @"C:\other\file2.txt", 200, DateTime.UtcNow)
        };

        // Act
        var batch = new OperationBatch(id, tool, label, created, operations);

        // Assert
        batch.Id.ShouldBe(id);
        batch.Tool.ShouldBe(tool);
        batch.Label.ShouldBe(label);
        batch.CreatedUtc.ShouldBe(created);
        batch.Status.ShouldBe(BatchStatus.Planned);
        batch.Operations.Count.ShouldBe(2);
        batch.OperationCount.ShouldBe(2);
        batch.TotalSize.ShouldBe(300);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_ShouldThrowForInvalidTool(string? tool)
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => 
            new OperationBatch(Guid.NewGuid(), tool!, "label", DateTime.UtcNow, Array.Empty<FileOperation>()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_ShouldThrowForInvalidLabel(string? label)
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => 
            new OperationBatch(Guid.NewGuid(), "tool", label!, DateTime.UtcNow, Array.Empty<FileOperation>()));
    }

    [Fact]
    public void Constructor_ShouldThrowForNullOperations()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new OperationBatch(Guid.NewGuid(), "tool", "label", DateTime.UtcNow, null!));
    }

    [Fact]
    public void Create_ShouldCreateBatchWithNewIdAndCurrentTime()
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow;
        var operations = new[]
        {
            FileOperation.Rename(@"C:\test\file.txt", @"C:\test\newfile.txt", 100, DateTime.UtcNow)
        };

        // Act
        var batch = OperationBatch.Create("Test Tool", "Test operation", operations);
        var afterCreate = DateTime.UtcNow;

        // Assert
        batch.Id.ShouldNotBe(Guid.Empty);
        batch.Tool.ShouldBe("Test Tool");
        batch.Label.ShouldBe("Test operation");
        batch.CreatedUtc.ShouldBeInRange(beforeCreate, afterCreate);
        batch.Status.ShouldBe(BatchStatus.Planned);
    }

    [Fact]
    public void UpdateStatus_ShouldUpdateStatusAndTimestamp()
    {
        // Arrange
        var batch = OperationBatch.Create("tool", "label", Array.Empty<FileOperation>());
        var beforeUpdate = DateTime.UtcNow;

        // Act
        batch.UpdateStatus(BatchStatus.Running, "Starting execution");
        var afterUpdate = DateTime.UtcNow;

        // Assert
        batch.Status.ShouldBe(BatchStatus.Running);
        batch.Notes.ShouldBe("Starting execution");
        batch.UpdatedUtc.ShouldNotBeNull();
        batch.UpdatedUtc!.Value.ShouldBeInRange(beforeUpdate, afterUpdate);
    }

    [Theory]
    [InlineData(BatchStatus.Complete, true)]
    [InlineData(BatchStatus.Failed, true)]
    [InlineData(BatchStatus.Planned, false)]
    [InlineData(BatchStatus.Running, false)]
    [InlineData(BatchStatus.Cancelled, false)]
    [InlineData(BatchStatus.Undone, false)]
    [InlineData(BatchStatus.PartiallyUndone, false)]
    public void CanUndo_ShouldReturnCorrectValue(BatchStatus status, bool canUndo)
    {
        // Arrange
        var batch = OperationBatch.Create("tool", "label", Array.Empty<FileOperation>());
        batch.UpdateStatus(status);

        // Act & Assert
        batch.CanUndo.ShouldBe(canUndo);
    }

    [Fact]
    public void ConflictsWith_ShouldDetectPathOverlaps()
    {
        // Arrange
        var batch1 = OperationBatch.Create("tool1", "label1", new[]
        {
            FileOperation.Move(@"C:\test\file1.txt", @"C:\dest\file1.txt", 100, DateTime.UtcNow),
            FileOperation.Rename(@"C:\test\file2.txt", @"C:\test\renamed.txt", 200, DateTime.UtcNow)
        });

        var batch2 = OperationBatch.Create("tool2", "label2", new[]
        {
            FileOperation.Move(@"C:\dest\file1.txt", @"C:\other\file1.txt", 100, DateTime.UtcNow)
        });

        // Act & Assert
        batch1.ConflictsWith(batch2).ShouldBeTrue();
        batch2.ConflictsWith(batch1).ShouldBeTrue();
    }

    [Fact]
    public void ConflictsWith_ShouldReturnFalseForNonOverlappingBatches()
    {
        // Arrange
        var batch1 = OperationBatch.Create("tool1", "label1", new[]
        {
            FileOperation.Move(@"C:\test\file1.txt", @"C:\dest\file1.txt", 100, DateTime.UtcNow)
        });

        var batch2 = OperationBatch.Create("tool2", "label2", new[]
        {
            FileOperation.Move(@"C:\other\file2.txt", @"C:\different\file2.txt", 200, DateTime.UtcNow)
        });

        // Act & Assert
        batch1.ConflictsWith(batch2).ShouldBeFalse();
        batch2.ConflictsWith(batch1).ShouldBeFalse();
    }

    [Fact]
    public void ConflictsWith_ShouldReturnFalseForNullBatch()
    {
        // Arrange
        var batch = OperationBatch.Create("tool", "label", new[]
        {
            FileOperation.Move(@"C:\test\file.txt", @"C:\dest\file.txt", 100, DateTime.UtcNow)
        });

        // Act & Assert
        batch.ConflictsWith(null!).ShouldBeFalse();
    }

    [Fact]
    public void Operations_ShouldBeReadOnly()
    {
        // Arrange
        var operations = new List<FileOperation>
        {
            FileOperation.Rename(@"C:\test\file.txt", @"C:\test\newfile.txt", 100, DateTime.UtcNow)
        };
        var batch = OperationBatch.Create("tool", "label", operations);

        // Act - Modifying original list shouldn't affect batch
        operations.Clear();

        // Assert
        batch.Operations.Count.ShouldBe(1);
        batch.OperationCount.ShouldBe(1);
    }
}