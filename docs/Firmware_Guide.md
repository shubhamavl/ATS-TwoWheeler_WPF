# Firmware Documentation (STM32)

## 1. Architecture & Flow
The firmware is built on a **1kHz deterministic control loop**.
*   **Main Loop**: Executed in `System_Main_Loop_1kHz()` (in `main.c`).
*   **Cycle Time**: 1ms.
*   **Operation**:
    1.  **Poll ADC**: Checks if new data is available (`System_Process_Raw_Data`).
    2.  **Process CAN**: Handles incoming commands and streams data (`System_Process_CAN_Communication`).
    3.  **Update Status**: Aggregates system health and mode info (`System_Update_Status`).
    4.  **Blink LED**: Heartbeat indication (2Hz).

## 2. Measurement & Control Logic

### Relay Logic (Brake vs Weight)
The system uses a relay to switch the input source for **Channel 0**.
*   **Pin**: `PB12` (Active HIGH).
*   **Function**:
    *   **Logic 0 (Weight Mode)**: Relay OFF. Input connected to Load Cell.
    *   **Logic 1 (Brake Mode)**: Relay ON. Input connected to Brake Force Sensor.
*   **Settling Time**: `RELAY_SETTLING_TIME_MS` (20ms) is strictly enforced after switching to allow mechanical bounce to settle.
*   **Control**: Managed by `System_Set_Mode()` in `main.c`.

### ADC System (Dual Mode)
The firmware supports two ADC backends, abstracted via `adc_unified.c`.
1.  **Internal ADC (12-bit)**:
    *   **Rate**: 1kHz (DMA).
    *   **Format**: Unsigned `0 - 4095` per channel.
    *   **Data Size**: 2 bytes (unsigned int) on CAN.
2.  **ADS1115 (16-bit)**:
    *   **Rate**: 250Hz (Limited by I2C speed).
    *   **Format**: Signed `-32768 to +32767`.
    *   **Data Size**: 4 bytes (signed int32) on CAN.

### System Modes
*   **Weight Mode**:
    *   Reads **ALL 4 Channels** (Ch0-Ch3).
    *   Sums them: `Total = Ch0 + Ch1 + Ch2 + Ch3`.
    *   Used for total vehicle weight.
*   **Brake Mode (Turbo)**:
    *   Reads **ONLY Channel 0**.
    *   Ignores Ch1-3 to maximize sampling rate on ADS1115.
    *   `Total = Ch0`.

## 3. CAN Telemetry
| ID | Name | Direction | Description |
| :--- | :--- | :--- | :--- |
| `0x200` | **Total Raw Data** | TX | Streamed ADC value (2 or 4 bytes). |
| `0x040` | **Start Stream** | RX | Payload: Rate (1=100Hz, 2=500Hz, 3=1kHz). |
| `0x044` | **Stop Stream** | RX | Stops all data streaming. |
| `0x050` | **Set Mode** | RX | Payload: 0=Weight, 1=Brake. |
| `0x300` | **System Status** | TX | 6-byte packed status (Mode, Relay, Uptime). |

## 4. Debugging Guide

### Live Expressions (STM32CubeIDE)
Monitor these variables for real-time debugging:
*   `g_perf`: Shows `can_tx_hz` and `adc_sample_hz`.
*   `g_raw_data`: Shows current `total_raw` and per-channel values.
*   `g_relay_state`: `1` = Relay ON (Brake), `0` = OFF.
*   `g_system_status`: Error flags and current mode.
*   `g_force_bootloader`: Should be `0` unless a boot update was requested.

### Global Variables
*   `g_ats_raw_data`: The atomic data structure holding latest sensor readings.
*   `g_current_mode`: Tracks `ADC_MODE_INTERNAL` vs `ADC_MODE_ADS1115`.
*   `adc_dma_buffer`: Raw DMA buffer for internal ADC.
