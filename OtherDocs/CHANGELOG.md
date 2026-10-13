# Changelog

All notable changes to the One Click project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Project structure with layered .NET 8 architecture
- Core domain models for file operations and batches
- SQLite-based journaling system for operation tracking
- Comprehensive test infrastructure with property-based testing
- CI/CD pipeline with coverage and security scanning
- Architecture tests to enforce dependency rules

### Changed
- Migrated from Python/tkinter prototype to professional .NET/WPF implementation

### Security
- Implemented path traversal protection
- Added input validation and sanitization
- Configured security scanning in CI pipeline

## [0.1] - 2026-10-06

### Added
- Initial Python/tkinter prototype
- Basic bulk rename functionality
- Folder organization by file type
- Storage analysis with file scanning
- Simple journaling and undo system
- Proof of concept for core user flows