# ATS Two-Wheeler System

## System Architecture

The **ATS Two-Wheeler System** is a distributed embedded control system designed for high-precision brake force and weight measurement. It connects a physical test bench to a PC application via a robust industrial CAN bus.

```mermaid
graph TD
    subgraph HostPC ["Host PC (Windows)"]
        UI["WPF User Interface"]
        Sim["Simulator (Optional)"]
        VSPE["Virtual Serial Port"]
        
        UI <-->|CAN Protocol v0.1| USB_CAN["USB-CAN Adapter"]
        UI <-->|Virtual COM| VSPE
        VSPE <-->|Virtual COM| Sim
    end

    subgraph Embedded ["Embedded System (STM32F302)"]
        USB_CAN <-->|CAN Bus (250kbps)| CAN_P["CAN Peripheral"]
        
        CAN_P -->|RX: Commands| FW_Main["Main Loop - 1kHz"]
        FW_Main -->|TX: Telemetry| CAN_P
        
        subgraph Firmware ["Firmware Components"]
            FW_Main -->|Control| Relay["Relay Logic"]
            FW_Main -->|Acquire| ADC_Unified["Unified ADC Driver"]
            
            ADC_Unified -->|Mode 0| ADC_Int["Internal ADC (12-bit)"]
            ADC_Unified -->|Mode 1| ADS1115["ADS1115 (16-bit)"]
            
            Boot["Bootloader"] -.->|Update| FW_Main
        end
        
        Relay -->|Switch| Sensor_In["Sensor Input"]
        Sensor_In --> ADC_Int
        Sensor_In --> ADS1115
    end

    subgraph Hardware ["Hardware"]
        LoadCell["Load Cells (x4)"]
        BrakeForce["Brake Force Sensor"]
        
        LoadCell -->|Weight Mode| Sensor_In
        BrakeForce -->|Brake Mode| Sensor_In
    end
```

## Component Overview

1.  **STM32 Firmware**: 
    *   Runs a **1kHz deterministic loop**.
    *   Handles sensor acquisition (Internal/External ADC).
    *   Manages Relay switching for Brake/Weight modes.
    *   Streams telemetry via CAN.
2.  **STM32 Bootloader**:
    *   Fail-safe update mechanism.
    *   Listening on CAN ID `0x51x`.
    *   Activated via Magic Flag in RTC Backup Register.
3.  **PC Application**:
    *   WPF-based dashboard.
    *   Real-time charting and calibration.
    *   Manages firmware updates and configuration.

## Key Repositories
*   **Firmware**: `ATS-TwoWheeler_Dev`
*   **Bootloader**: `STM32_Bootloader`
*   **UI**: `ATS-TwoWheeler_WPF`
*   **Simulator**: `ATS_TwoWheeler_Simulator`
