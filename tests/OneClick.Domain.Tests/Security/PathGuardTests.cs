using OneClick.Domain.Security;
using Shouldly;
using Xunit;

namespace OneClick.Domain.Tests.Security;

public class PathGuardTests
{
    [Fact]
    public void Check_ShouldAllowRegularUserPath()
    {
        // Arrange
        var guard = new PathGuard();
        var userDocuments = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "test.txt");

        // Act
        var result = guard.Check(userDocuments, GuardIntent.WriteTarget);

        // Assert
        result.Allowed.ShouldBeTrue();
    }

    [Fact]
    public void Check_ShouldDenySystemDirectory()
    {
        // Arrange
        var guard = new PathGuard();
        var systemPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "test.txt");

        // Act
        var result = guard.Check(systemPath, GuardIntent.WriteTarget);

        // Assert
        result.Allowed.ShouldBeFalse();
        result.Reason.ShouldContain("protected location");
    }

    [Fact]
    public void Check_ShouldDenyProgramFiles()
    {
        // Arrange
        var guard = new PathGuard();
        var programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "test.txt");

        // Act
        var result = guard.Check(programFilesPath, GuardIntent.WriteTarget);

        // Assert
        result.Allowed.ShouldBeFalse();
        result.Reason.ShouldContain("protected location");
    }

    [Fact]
    public void Check_ShouldDenyUserProfileRoot()
    {
        // Arrange
        var guard = new PathGuard();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Act
        var result = guard.Check(userProfile, GuardIntent.WriteTarget);

        // Assert
        result.Allowed.ShouldBeFalse();
        result.Reason.ShouldContain("user profile root");
    }

    [Fact]
    public void Check_ShouldAllowUserProfileSubdirectory()
    {
        // Arrange
        var guard = new PathGuard();
        var userSubfolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "test.txt");

        // Act
        var result = guard.Check(userSubfolder, GuardIntent.WriteTarget);

        // Assert
        result.Allowed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    public void Check_ShouldDenyReservedDeviceNames(string deviceName)
    {
        // Arrange
        var guard = new PathGuard();
        var devicePath = Path.Combine(@"C:\temp", deviceName);

        // Act
        var result = guard.Check(devicePath, GuardIntent.WriteTarget);

        // Assert
        result.Allowed.ShouldBeFalse();
        result.Reason.ShouldContain("reserved device name");
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"D:\")]
    public void Check_ShouldDenyDriveRoots(string drivePath)
    {
        // Arrange
        var guard = new PathGuard();

        // Act
        var result = guard.Check(drivePath, GuardIntent.WriteTarget);

        // Assert
        result.Allowed.ShouldBeFalse();
        result.Reason.ShouldContain("drive root");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Check_ShouldDenyEmptyOrNullPath(string? path)
    {
        // Arrange
        var guard = new PathGuard();

        // Act
        var result = guard.Check(path!, GuardIntent.WriteTarget);

        // Assert
        result.Allowed.ShouldBeFalse();
        result.Reason.ShouldContain("empty");
    }

    [Fact]
    public void Check_ShouldAllowScanOnlyForProtectedPaths()
    {
        // Arrange
        var guard = new PathGuard();
        var systemPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");

        // Act
        var writeResult = guard.Check(systemPath, GuardIntent.WriteTarget);
        var scanResult = guard.Check(systemPath, GuardIntent.ScanOnly);

        // Assert
        writeResult.Allowed.ShouldBeFalse();
        scanResult.Allowed.ShouldBeTrue(); // Scanning should be allowed even in protected areas
    }

    [Fact]
    public void Check_ShouldHandleRelativePaths()
    {
        // Arrange
        var guard = new PathGuard();
        var relativePath = @"..\..\..\Windows\System32\test.txt";

        // Act
        var result = guard.Check(relativePath, GuardIntent.WriteTarget);

        // Assert
        result.Allowed.ShouldBeFalse(); // Should resolve to system directory and be denied
    }

    [Fact]
    public void Check_ShouldHandlePathTraversalAttempts()
    {
        // Arrange
        var guard = new PathGuard();
        var traversalPath = @"C:\Users\test\..\..\..\Windows\System32\test.txt";

        // Act
        var result = guard.Check(traversalPath, GuardIntent.WriteTarget);

        // Assert
        result.Allowed.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_ShouldAcceptCustomProtectedPaths()
    {
        // Arrange
        var customProtected = new[] { @"C:\CustomProtected", @"D:\AnotherProtected" };
        var guard = new PathGuard(customProtected);

        // Act
        var result1 = guard.Check(@"C:\CustomProtected\test.txt", GuardIntent.WriteTarget);
        var result2 = guard.Check(@"C:\RegularFolder\test.txt", GuardIntent.WriteTarget);

        // Assert
        result1.Allowed.ShouldBeFalse();
        result2.Allowed.ShouldBeTrue();
    }

    [Fact]
    public void Check_ShouldBeThreadSafe()
    {
        // Arrange
        var guard = new PathGuard();
        var testPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "test.txt");
        var results = new bool[100];

        // Act - Multiple threads checking simultaneously
        Parallel.For(0, 100, i =>
        {
            var result = guard.Check($"{testPath}{i}", GuardIntent.WriteTarget);
            results[i] = result.Allowed;
        });

        // Assert - All should be allowed
        results.ShouldAllBe(allowed => allowed);
    }
}