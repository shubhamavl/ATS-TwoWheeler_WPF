# PC Application & Simulator Documentation

## 1. WPF UI Architecture
The PC Application (`ATS-TwoWheeler_WPF`) is built on **.NET 8** and **WPF** using the **MVVM** pattern.

### Key Components
*   **CANCommunication**: Handled by `CANService.cs`.
    *   Supports **USB-CAN-A** (Serial-based) and **PCAN-USB** adapters.
    *   Implements the v0.1 Protocol (Semantic IDs).
    *   Manages connection state and auto-reconnection.
*   **Data Processing**:
    *   Parses `0x200` Raw ADC messages.
    *   Applies **Linear Calibration** (y = mx + c) defined in `CalibrationDialog`.
    *   Handles **Taring** (Zeroing) logic internally (not on firmware).
*   **Bootloader Manager**:
    *   Dedicated window (`BootloaderManagerWindow`) for firmware updates.
    *   Orchestrates the update state machine (Enter -> Ping -> Transfer -> Reset).

## 2. Simulator Guide
The **ATS Two-Wheeler Simulator** is a software twin of the firmware.

### Purpose
*   Validate UI plotting and latency without physical hardware.
*   Test edge cases (error injection, noise) that are hard to reproduce.

### Usage
1.  **Launch** `ATS_TwoWheeler_Simulator.exe`.
2.  **Select Adapter**:
    *   Use **VSPE** (Virtual Serial Port Emulator) to bridge Simulator <-> UI.
    *   Simulator on `COM10`, UI on `COM11` (for example).
3.  **Connect**: Click the green plug icon.
4.  **Control**:
    *   **Pattern**: Select Static, Sine Wave, or Ramp.
    *   **Noise**: Inject random noise to test filtering.
    *   **Errors**: Force error flags to test UI handling.

## 3. Troubleshooting
| Symptom | Probable Cause | Fix |
| :--- | :--- | :--- |
| **No Data (0 Hz)** | Wrong COM Port or wiring. | Check Device Manager and CAN High/Low wiring. |
| **"Bootloader Not Responding"** | STM32 not in boot mode. | Retry "Enter Bootloader" or use hardware reset. |
| **Drifting Values** | Sensor warm-up or loose wire. | Allow 15 min warm-up; check Load Cell connections. |
| **Relay Clicking** | Mode switching too fast. | Firmware enforces 20ms settling time. |
