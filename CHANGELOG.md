# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- LICENSE file (MIT License)
- CONTRIBUTING.md with contribution guidelines
- CHANGELOG.md for tracking project changes
- CODE_OF_CONDUCT.md for community guidelines
- SECURITY.md with security policy and vulnerability reporting
- IMPROVEMENTS.md with comprehensive project improvement summary
- GitHub Actions workflows for CI/CD
- GitHub issue and PR templates
- Comprehensive XML documentation for code models

### Changed
- Improved project documentation
- Enhanced .gitignore with comprehensive rules
- Enhanced .editorconfig with code style and naming conventions
- README updated with badges and documentation links

### Fixed
- Security vulnerability in actions/download-artifact (updated to v4.1.3)
- Added parameter validation to critical methods (RarArchiveEngine, PasswordUtility)
- Added ConfigureAwait(false) to all async operations for better performance
- Enhanced code quality and documentation

## [1.0.0] - 2024-12-26

### Added
- Cross-platform support using Avalonia UI
- Batch compression to RAR/ZIP formats
- Batch decompression of RAR/ZIP files
- MD5-based random password generation
- Custom password support
- Volume compression support
- Solid archive option
- Multiple compression levels
- Password query functionality
- Advanced feature unlock mechanism
- Multi-language support (Chinese, English)
- Command-line interface for headless operations
- Comprehensive logging system
- Drag-and-drop file support

### Technical
- Built with Avalonia UI 11.3.10
- Target framework: .NET 10.0
- MVVM architecture using CommunityToolkit.Mvvm
- Cross-platform RAR/UnRAR integration
- Localization support

---

**Note**: This is a cross-platform rewrite of the original WinForms-based batch compression tool.
