using OneClick.Domain.FileSystem;
using Shouldly;
using Xunit;

namespace OneClick.Domain.Tests.FileSystem;

public class ConflictResolverTests
{
    [Fact]
    public void GetUniquePath_ShouldReturnOriginalIfNoConflict()
    {
        // Arrange
        var resolver = new ConflictResolver(_ => false); // Nothing exists
        var desiredPath = @"C:\test\file.txt";

        // Act
        var result = resolver.GetUniquePath(desiredPath);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(desiredPath);
    }

    [Fact]
    public void GetUniquePath_ShouldAddSuffixForConflict()
    {
        // Arrange
        var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\test\file.txt"
        };
        var resolver = new ConflictResolver(existingFiles.Contains);
        var desiredPath = @"C:\test\file.txt";

        // Act
        var result = resolver.GetUniquePath(desiredPath);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(@"C:\test\file (1).txt");
    }

    [Fact]
    public void GetUniquePath_ShouldIncrementSuffixForMultipleConflicts()
    {
        // Arrange
        var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\test\file.txt",
            @"C:\test\file (1).txt",
            @"C:\test\file (2).txt"
        };
        var resolver = new ConflictResolver(existingFiles.Contains);
        var desiredPath = @"C:\test\file.txt";

        // Act
        var result = resolver.GetUniquePath(desiredPath);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(@"C:\test\file (3).txt");
    }

    [Fact]
    public void GetUniquePath_ShouldHandleFilesWithoutExtension()
    {
        // Arrange
        var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\test\README"
        };
        var resolver = new ConflictResolver(existingFiles.Contains);
        var desiredPath = @"C:\test\README";

        // Act
        var result = resolver.GetUniquePath(desiredPath);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(@"C:\test\README (1)");
    }

    [Fact]
    public void GetUniquePath_ShouldAvoidPlannedNames()
    {
        // Arrange
        var resolver = new ConflictResolver(_ => false); // No existing files
        resolver.RegisterPlannedName(@"C:\test\file.txt");

        // Act
        var result = resolver.GetUniquePath(@"C:\test\file.txt");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(@"C:\test\file (1).txt");
    }

    [Fact]
    public void GetUniquePath_ShouldRemoveExistingSuffix()
    {
        // Arrange
        var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\test\file (5).txt"
        };
        var resolver = new ConflictResolver(existingFiles.Contains);
        var desiredPath = @"C:\test\file (5).txt"; // Already has suffix

        // Act
        var result = resolver.GetUniquePath(desiredPath);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(@"C:\test\file (1).txt"); // Should use base name
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GetUniquePath_ShouldFailForInvalidPath(string? path)
    {
        // Arrange
        var resolver = new ConflictResolver(_ => false);

        // Act
        var result = resolver.GetUniquePath(path!);

        // Assert
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void RegisterPlannedName_ShouldPreventConflictsWithPlannedFiles()
    {
        // Arrange
        var resolver = new ConflictResolver(_ => false);
        
        // Act - Register multiple planned names
        resolver.RegisterPlannedName(@"C:\test\file1.txt");
        resolver.RegisterPlannedName(@"C:\test\file2.txt");
        
        var result1 = resolver.GetUniquePath(@"C:\test\file1.txt");
        var result2 = resolver.GetUniquePath(@"C:\test\file2.txt");

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result1.Value.ShouldBe(@"C:\test\file1 (1).txt");
        
        result2.IsSuccess.ShouldBeTrue();
        result2.Value.ShouldBe(@"C:\test\file2 (1).txt");
    }

    [Fact]
    public void ClearPlannedNames_ShouldRemoveAllPlannedNames()
    {
        // Arrange
        var resolver = new ConflictResolver(_ => false);
        resolver.RegisterPlannedName(@"C:\test\file.txt");

        // Act
        resolver.ClearPlannedNames();
        var result = resolver.GetUniquePath(@"C:\test\file.txt");

        // Assert - Should now be able to use the original name
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(@"C:\test\file.txt");
    }

    [Fact]
    public void GetRenameStrategy_ShouldReturnDirectForDifferentDirectories()
    {
        // Arrange
        var resolver = new ConflictResolver(_ => false);
        var source = @"C:\source\file.txt";
        var destination = @"C:\dest\file.txt";

        // Act
        var result = resolver.GetRenameStrategy(source, destination);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Type.ShouldBe(RenameType.Direct);
        result.Value.FinalPath.ShouldBe(destination);
    }

    [Fact]
    public void GetRenameStrategy_ShouldReturnNoOperationForSameName()
    {
        // Arrange
        var resolver = new ConflictResolver(_ => false);
        var path = @"C:\test\file.txt";

        // Act
        var result = resolver.GetRenameStrategy(path, path);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Type.ShouldBe(RenameType.None);
    }

    [Fact]
    public void GetRenameStrategy_ShouldReturnTwoPhaseForCaseChange()
    {
        // Arrange
        var resolver = new ConflictResolver(_ => false);
        var source = @"C:\test\file.txt";
        var destination = @"C:\test\FILE.TXT"; // Case change only

        // Act
        var result = resolver.GetRenameStrategy(source, destination);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Type.ShouldBe(RenameType.TwoPhase);
        result.Value.TemporaryPath.ShouldNotBeNull();
        result.Value.FinalPath.ShouldBe(destination);
        result.Value.TemporaryPath.ShouldContain(".oneclick-tmp-");
    }

    [Fact]
    public void GetRenameStrategy_ShouldReturnDirectForDifferentNames()
    {
        // Arrange
        var resolver = new ConflictResolver(_ => false);
        var source = @"C:\test\file.txt";
        var destination = @"C:\test\newfile.txt";

        // Act
        var result = resolver.GetRenameStrategy(source, destination);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Type.ShouldBe(RenameType.Direct);
        result.Value.FinalPath.ShouldBe(destination);
    }

    [Fact]
    public void GetRenameStrategy_ShouldHandleNullOrEmptyPaths()
    {
        // Arrange
        var resolver = new ConflictResolver(_ => false);

        // Act
        var result1 = resolver.GetRenameStrategy("", @"C:\test\file.txt");
        var result2 = resolver.GetRenameStrategy(@"C:\test\file.txt", null!);

        // Assert
        result1.IsSuccess.ShouldBeFalse();
        result2.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void GetUniquePath_ShouldFailAfterManyAttempts()
    {
        // Arrange - All possible suffixes exist
        var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i <= 10000; i++)
        {
            if (i == 0)
                existingFiles.Add(@"C:\test\file.txt");
            else
                existingFiles.Add($@"C:\test\file ({i}).txt");
        }
        
        var resolver = new ConflictResolver(existingFiles.Contains);

        // Act
        var result = resolver.GetUniquePath(@"C:\test\file.txt");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Message.ShouldContain("Unable to generate unique name");
    }
}