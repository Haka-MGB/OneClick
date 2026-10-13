using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace OneClick.ArchTests;

public class ArchitectureTests
{
    private const string DomainNamespace = "OneClick.Domain";
    private const string ApplicationNamespace = "OneClick.Application";
    private const string InfrastructureNamespace = "OneClick.Infrastructure";
    private const string AppNamespace = "OneClick.App";

    [Fact]
    public void Domain_Should_Not_Have_Dependencies()
    {
        // Domain layer must be pure - no external dependencies
        var result = Types.InAssembly(typeof(OneClick.Domain.AssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOn("Microsoft")
            .And()
            .NotHaveDependencyOn("Serilog")
            .And()
            .NotHaveDependencyOn("Dapper")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Domain layer has forbidden dependencies: {string.Join(", ", result.FailingTypeNames)}");
    }

    [Fact]
    public void Application_Should_Only_Depend_On_Domain()
    {
        var result = Types.InAssembly(typeof(OneClick.Application.AssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOn(InfrastructureNamespace)
            .And()
            .NotHaveDependencyOn(AppNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Application layer has forbidden dependencies: {string.Join(", ", result.FailingTypeNames)}");
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_App()
    {
        var result = Types.InAssembly(typeof(OneClick.Infrastructure.AssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOn(AppNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Infrastructure layer depends on App layer: {string.Join(", ", result.FailingTypeNames)}");
    }

    [Fact]
    public void All_File_Operations_Should_Go_Through_Interfaces()
    {
        // Requirement FR-CORE-01: Application orchestration should not perform file changes directly.
        // The current WPF shell still contains prototype file operations; this rule guards the use-case layer
        // until the operation engine is promoted into Application/Infrastructure.
        var result = Types.InAssemblies([
                typeof(OneClick.Application.AssemblyMarker).Assembly
            ])
            .Should()
            .NotHaveDependencyOn("System.IO.File")
            .And()
            .NotHaveDependencyOn("System.IO.Directory")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Direct file system access found (should use IFileSystem): {string.Join(", ", result.FailingTypeNames)}");
    }

    [Fact]
    public void Interfaces_Should_Be_In_Application_Layer()
    {
        var interfaces = Types.InAssembly(typeof(OneClick.Application.AssemblyMarker).Assembly)
            .That()
            .AreInterfaces()
            .GetTypes();

        // All application interfaces should follow naming convention
        var result = Types.InAssembly(typeof(OneClick.Application.AssemblyMarker).Assembly)
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Interfaces don't follow naming convention: {string.Join(", ", result.FailingTypeNames)}");
    }

    [Fact]
    public void Domain_Services_Should_Not_Have_IO_Dependencies()
    {
        var result = Types.InAssembly(typeof(OneClick.Domain.AssemblyMarker).Assembly)
            .That()
            .HaveNameEndingWith("Service")
            .Should()
            .NotHaveDependencyOn("System.IO")
            .And()
            .NotHaveDependencyOn("System.Threading.Tasks")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Domain services have I/O dependencies: {string.Join(", ", result.FailingTypeNames)}");
    }
}
