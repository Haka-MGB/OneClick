# ADR-001: Layered Architecture with Dependency Inversion

## Status
Accepted

## Context
The One Click application needs a maintainable architecture that supports:
- Clear separation of concerns
- Testability with isolated unit tests
- Future extensibility (additional tools, UI frameworks)
- Compliance with the "never lose data" principle through a robust core

## Decision
We will use a layered architecture with dependency inversion:

```
OneClick.App (WPF UI Layer)
├── OneClick.Application (Use Cases / Application Services)
├── OneClick.Domain (Pure Business Logic)
└── OneClick.Infrastructure (External Concerns)
```

### Layer Responsibilities

**Domain Layer (`OneClick.Domain`)**
- Pure business logic with no external dependencies
- Core models: `FileOperation`, `OperationBatch`, `PathGuard`
- Business rules and invariants
- Domain services that contain no I/O

**Application Layer (`OneClick.Application`)**
- Use cases and application services
- Interfaces for infrastructure concerns
- DTOs and command/query models
- Orchestration of domain logic

**Infrastructure Layer (`OneClick.Infrastructure`)**
- File system operations (via System.IO.Abstractions)
- SQLite database access
- Windows registry access
- External API integrations
- Implements interfaces defined in Application layer

**App Layer (`OneClick.App`)**
- WPF UI components and ViewModels
- Dependency injection container configuration
- Application bootstrapping
- UI-specific services

### Dependency Rules
- Domain references nothing (pure)
- Application references only Domain
- Infrastructure references Application and Domain
- App references all layers through DI container
- All cross-layer communication uses interfaces

## Consequences

### Positive
- Clear separation enables focused testing
- Domain logic remains pure and easily testable
- Infrastructure can be swapped (e.g., different file systems for testing)
- Architecture tests can enforce dependency rules
- Supports the critical "Operation Engine" pattern from requirements

### Negative
- More initial complexity than a simple layered approach
- Requires discipline to maintain boundaries
- Interface explosion for every external concern

### Risks Mitigated
- Accidental coupling between UI and file operations
- Untestable business logic
- Violation of the "single Operation Engine" requirement (FR-CORE-01)

## Enforcement
Architecture tests in `OneClick.ArchTests` will verify:
- Domain has no external dependencies
- Layers only reference allowed dependencies
- All file operations go through defined interfaces