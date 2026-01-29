# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-01-29

### Added
- **Brake Mode Integration**: Full support for two-wheeler brake testing and weight monitoring.
- **System Status Panel**: Real-time monitoring of performance, firmware version, and system health.
- **Improved Calibration**: Multi-point calibration system with a dedicated wizard and quality indicators.
- **Bootloader Manager**: CAN-based firmware update functionality with progress tracking.
- **Production Log Viewer**: Advanced logging interface with filtering and export options.
- **Service Registry**: Introduced dependency injection for better service management.
- **Configuration Viewer**: Dedicated window to inspect internal application and system settings.

### Changed
- **UI Refresh**: Significant increase in button sizes and font readability throughout the application.
- **MVVM Refactoring**: Complete restructuring of the application architecture to follow MVVM patterns.
- **Default Settings**: Set default CAN baud rate to 250 kbps for standard industrial compatibility.
- **Firmware Version Display**: Simplified display by removing redundant "FW:" prefixes.

### Fixed
- Resolved critical build errors (CS0106, CS0535) and interface implementation issues.
- Fixed live data dashboard refresh and synchronization problems.
- Removed UI redundancies in filtering settings (EMA filter checkbox).
- Addressed performance bottlenecks in logging and data processing.

## [0.1.0] - 2026-01-25

### Added
- **Initial Release**: Core WPF application for two-wheeler weight measurement.
- **CAN Connectivity**: Support for USB-CAN-A and PCAN adapters.
- **Calibration System**: Initial linear calibration implementation.
- **Weight Monitoring**: Real-time display for left/right wheel weights.
- **Data Logging**: Basic CSV and production logging functionality.
- **Bootloader Core**: Foundations for CAN-based firmware updates.
