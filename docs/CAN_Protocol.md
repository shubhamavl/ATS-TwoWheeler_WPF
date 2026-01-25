# CAN Protocol Specification

## Overview
The ATS Two-Wheeler system uses a custom **CAN Protocol**.
*   **Baud Rate**: 250 kbps
*   **Addressing**: Standard 11-bit IDs
*   **Endianness**: Little Endian (Intel)
*   **DLC**: Variable (minimized for bandwidth)

---

## 1. Telemetry Messages (TX)
*Sent from STM32 to PC*

### 0x200 - Total Raw Data
Real-time sensor data stream. Format depends on active ADC Mode.

### 0x200 - Total Raw Data
Real-time sensor data stream. Format depends on active ADC Mode.

| Mode | Byte | Bit | Name | Type | Scaling | Description |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Internal (12-bit)** | 0-1 | 0-15 | Total Raw | `uint16` | 0-16380 | Sum of 4 channels (0-4095 each). |
| **ADS1115 (16-bit)** | 0-3 | 0-31 | Total Raw | `int32` | Raw Count | Signed value. Weigh Mode: Sum of 4. Brake Mode: Ch0 only. |

### 0x300 - System Status
Periodic health and status heartbeat (1Hz or On-Demand).

| Byte | Bit | Name | Value | Description |
| :--- | :--- | :--- | :--- | :--- |
| **0** | 0-1 | System Status | 0..3 | 0=OK, 1=Warning, 2=Error |
| | 2 | ADC Mode | 0/1 | 0=Internal, 1=ADS1115 |
| | 3 | Relay State | 0/1 | 0=Weight (OFF), 1=Brake (ON) |
| | 4-7 | Reserved | - | Reserved |
| **1** | 0-7 | Error Flags | Hex | 0x01=ADC Init, 0x02=CAN Init, 0x04=I2C |
| **2-5** | 0-31 | Uptime | `uint32` | Seconds | System uptime since reset |

### 0x301 - Firmware Version
Response to Version Request (0x033).

| Byte | Name | Description |
| :--- | :--- | :--- |
| 0 | Major | Major version |
| 1 | Minor | Minor version |
| 2 | Patch | Patch version |
| 3 | Build | Build number |

---

## 2. Control Commands (RX)
*Sent from PC to STM32*

### 0x040 - Start Stream
Begin broadcasting 0x200 data.

| Byte | Name | Value | Description |
| :--- | :--- | :--- | :--- |
| 0 | Rate Code | 1 | 100 Hz |
| | | 2 | 500 Hz |
| | | 3 | 1 kHz (Max) |
| | | 5 | 1 Hz (Slow) |

### 0x050 - Set System Mode
Control physical relay and logic.

| Byte | Name | Value | Description |
| :--- | :--- | :--- | :--- |
| 0 | Mode | 0 | **Weight Mode**: Relay OFF, Read All Channels |
| | | 1 | **Brake Mode**: Relay ON, Read Ch0 Only (Turbo) |

### Simple Commands (No Payload)
These commands have DLC=0.

| ID | Name | Action |
| :--- | :--- | :--- |
| **0x044** | Stop Stream | Stops all 0x200 broadcasts immediately. |
| **0x030** | Set Internal | Switches to Internal ADC backend (12-bit). |
| **0x031** | Set ADS1115 | Switches to external ADS1115 backend (16-bit). |
| **0x032** | Req Status | STM32 immediately replies with 0x300. |
| **0x510** | Enter Boot | Request system reset into bootloader mode. |

---

## 3. Bootloader Protocol
*Shared IDs for Update Process*

### 0x513 - Begin Update (RX)
Start a new transfer session.

| Byte | Name | Type | Description |
| :--- | :--- | :--- | :--- |
| 0-3 | Size | `uint32` | Total size of firmware binary in bytes |

### 0x520 - Data Chunk (RX)
Firmware binary data.

| Byte | Name | Description |
| :--- | :--- | :--- |
| 0 | Seq | Rolling sequence number (0-255) |
| 1-7 | Data | 7 Bytes of raw firmware binary |

### 0x51B - Error Report (TX)
Sent if update fails.

| Byte | Name | Value | Description |
| :--- | :--- | :--- | :--- |
| 0 | Error Code | 0x01 | Sequence Mismatch |
| | | 0x02 | CRC Mismatch |
### 0x51B + Expanded Error Set
Error reporting (TX).

| ID | Name | Value | Description |
| :--- | :--- | :--- | :--- |
| **0x51B** | Seq Error | - | Sequence Mismatch. |
| **0x51D** | Size Error | - | Size Mismatch (Flash vs RX). |
| **0x51E** | Write Error | - | Flash Write Failed. |
| **0x51F** | Validate Fail | - | CRC Validation Failed. |
| **0x516** | Buf Overflow | - | RX Buffer Overflow. |

### Additional Bootloader Responses (TX)
| ID | Name | Description |
| :--- | :--- | :--- |
| **0x518** | Begin Resp | Acknowledgement of 0x513 Begin command. |
| **0x519** | Progress | Periodic progress update (0-100%). |
| **0x51A** | End Resp | Acknowledgement of 0x514 End command. |
| **0x51C** | Info Resp | Response to 0x511 Query Info. |

### Diagnostic Commands (RX)
| ID | Name | Description |
| :--- | :--- | :--- |
| **0x511** | Query Info | Request Bootloader version and state. |
