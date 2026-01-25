# Release Management

## Overview
This page tracks all official releases of the ATS Two-Wheeler System components. Always use the latest stable versions unless testing specific features.

---

## Firmware Releases

### Latest Stable Version
**v1.0.0** - Production Ready (14 Dec 2025)

### Version History

| Version | Date | Firmware Binary | Changes | Status |
| :--- | :--- | :--- | :--- | :--- |
| **v1.0.0** | 2025-12-14 | [ATS_FW_v1.0.0.bin](#) | Production release: 1kHz loop, Dual ADC, Relay control | Stable |
| v0.9.0 | 2025-12-01 | [ATS_FW_v0.9.0.bin](#) | Beta: CAN protocol v0.1, ADS1115 support | Beta |
| v0.8.0 | 2025-11-15 | [ATS_FW_v0.8.0.bin](#) | Alpha: Internal ADC only | Alpha |

### Firmware Features by Version

#### v1.0.0 (Current Stable)
- 1kHz deterministic control loop
- Dual ADC support (Internal 12-bit / ADS1115 16-bit)
- Relay control for Brake/Weight modes
- CAN Protocol v0.1 (Semantic IDs)
- On-demand streaming (1Hz, 100Hz, 500Hz, 1kHz)
- Moving average filter (4 samples)
- Production-ready error handling

---

## Bootloader Releases

### Latest Stable Version
**v1.0.0** - Production Ready (14 Dec 2025)

### Version History

| Version | Date | Bootloader Binary | Changes | Status |
| :--- | :--- | :--- | :--- | :--- |
| **v1.0.0** | 2025-12-14 | [Bootloader_v1.0.0.bin](#) | Production: Proactive Ping, Magic Flag Entry, LCD Support | Stable |
| v0.9.0 | 2025-12-01 | [Bootloader_v0.9.0.bin](#) | Beta: Basic CAN flashing support | Beta |

---

## PC Software Releases

### Latest Stable Version
**v1.0.0** - Production Ready (14 Dec 2025)

### Version History

| Version | Date | Installer | Changes | Status |
| :--- | :--- | :--- | :--- | :--- |
| **v1.0.0** | 2025-12-14 | [ATS_UI_v1.0.0_Release.zip](#) | Production: Bootloader Manager, Calibration, Taring | Stable |
| v0.9.0 | 2025-12-01 | [ATS_UI_v0.9.0_Release.zip](#) | Beta: Real-time charting, PCAN support | Beta |

### PC Software Features by Version

#### v1.0.0 (Current Stable)
- USB-CAN-A and PCAN adapter support
- Real-time data visualization (1kHz capable)
- Bootloader Manager (Firmware updates)
- Linear calibration (per-mode)
- Taring (software-based zeroing)
- Configuration persistence
- Production logging

---

## Simulator Releases

### Latest Version
**v0.1.0** - Initial Release (25 Dec 2025)

| Version | Date | Download | Changes |
| :--- | :--- | :--- | :--- |
| **v0.1.0** | 2025-12-25 | [ATS_Simulator_v0.1.0.zip](#) | Initial release: Pattern generation, Noise injection |

---

## Protocol Specifications

### CAN Protocol v0.1
- **Baud Rate**: 250 kbps
- **Frame Type**: Standard (11-bit ID)
- **Data Optimization**: Semantic IDs, minimal payload
- **Specification**: See Confluence Page: **CAN Protocol Specification**

---

## Installation Instructions

### Firmware Update
1. Download the **Firmware** binary.
2. Open **ATS Two-Wheeler UI** → **Settings** → **Bootloader Manager**.
3. Select the `.bin` file and click **Start Update**.
4. Wait for completion (~30 seconds).

### Bootloader Update
*Note: Bootloader updates typically require an ST-Link programmer.*
1. Connect ST-Link V2 to the SWD header.
2. Use STM32CubeProgrammer to flash `Bootloader_vX.X.X.bin` to address `0x08000000`.

### PC Software Installation
1. Download the **Release** installer.
2. Extract and run `ATS_TwoWheeler_WPF.exe`.
3. No installation required (portable).

---

## Compatibility Matrix

| Firmware | Bootloader | PC Software | Protocol | Compatible |
| :--- | :--- | :--- | :--- | :--- |
| v1.0.0 | v1.0.0 | v1.0.0 | v0.1 | Yes |
| v0.9.0 | v0.9.0 | v0.9.0 | v0.1 | Yes |
| v1.0.0 | v0.9.0 | v1.0.0 | v0.1 | Partial |

---

## Release Notes Archive
Detailed changelogs for each version:
- [Firmware CHANGELOG.md](file:///c:/Users/u32n08/git/ATS-TwoWheeler_Dev/CHANGELOG.md)
- [UI CHANGELOG.md](file:///c:/Users/u32n08/git/ATS-TwoWheeler_WPF/CHANGELOG.md)
