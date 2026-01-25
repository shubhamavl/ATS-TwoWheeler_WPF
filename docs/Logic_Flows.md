# ATS Two-Wheeler System: Logic & Flows

## Overview
This document visualizes the core logic and behavioral flows of the system. It is designed to explain *how* the system works conceptually, bridging the gap between hardware behavior and software logic.

---

## 1. System Startup Sequence
**Goal:** Ensure safe power-up, check for updates, and initialize sensors.

```mermaid
sequenceDiagram
    participant P as Power On
    participant B as Bootloader
    participant A as Application
    participant H as Hardware (Relay/ADC)

    P->>B: 3.3V Stable
    B->>B: Check RTC Backup Register
    alt Magic Flag == 0xDEADC0DE
        B->>B: Stay in Bootloader Mode
        B-->>User: Wait for CAN Commands
    else Magic Flag == 0 (Default)
        B->>A: Jump to Address 0x08008000
    end
    
    A->>H: Init HAL (Clocks, GPIO)
    A->>H: Init CAN (250 kbps)
    A->>H: Calibrate Internal ADC (Self-Cal)
    A->>H: Start Timer (1 kHz)
    
    loop Main Control Loop (1 kHz)
        A->>H: Read Sensors
        A->>A: Filter Data
        A->>User: Transmit CAN (0x200)
    end
```

---

## 2. Weight vs. Brake Mode Switching
**Goal:** Switch physical hardware (Relay) and software logic safely.

**Key Logic:**
1.  **Relay Switching**: Physical relays take time to move (~5-10ms). We wait **20ms** to be safe.
2.  **Filter Reset**: Switching modes changes the signal drastically. We must clear old "Weight" data before processing "Brake" data to avoid spikes.

```mermaid
stateDiagram-v2
    [*] --> WeightMode: Default Power-On
    
    state WeightMode {
        [*] --> RelayOFF
        RelayOFF --> Read4Channels: Valid
        Read4Channels --> SumValues
    }

    state BrakeMode {
        [*] --> RelayON
        RelayON --> ReadCh0Only: Valid
        ReadCh0Only --> TurboSpeed
    }

    WeightMode --> Transition: Rx Command (0x050=0x01)
    Transition --> BrakeMode: After 20ms Stable

    BrakeMode --> Transition: Rx Command (0x050=0x00)
    Transition --> WeightMode: After 20ms Stable

    state Transition {
        SwitchRelay
        Wait20ms
        ResetFilters
    }
```

---

## 3. ADC Data Pipeline
**Goal:** Convert analog voltage to clean, usable weight data.

**Concept:**
The system uses a **Dual-Backend** architecture. The main application logic doesn't care which hardware is used; it just asks for "Total Raw Data".

```mermaid
flowchart LR
    subgraph Input [Sensors]
        S1[Load Cell]
        S2[Brake Sensor]
    end

    subgraph Hardware [ADC Hardware]
        direction TB
        INT["Internal ADC<br/>(High Speed, 12-bit)"]
        EXT["ADS1115<br/>(High Precision, 16-bit)"]
    end

    subgraph Processing [Firmware Logic]
        F1["Moving Average Filter<br/>(Smooths Jitter)"]
        F2["Mode Selector<br/>(Weight Sum vs Brake Ch0)"]
    end

    subgraph Output [CAN Bus]
        MSG[CAN Message 0x200]
    end

    S1 --> INT
    S1 --> EXT
    S2 --> INT
    S2 --> EXT

    INT -.->|Mode 0 Selected| F1
    EXT -.->|Mode 1 Selected| F1

    F1 --> F2
    F2 --> MSG
```

---

## 4. Firmware Update Process (Bootloader)
**Goal:** Update the application firmware in the field without special hardware.

**Logic:**
The update is a handshake process. The PC tells the STM32 to restart, the STM32 waits for data, and then writes it to flash memory.

```mermaid
sequenceDiagram
    participant PC as PC App (UI)
    participant FW as Firmware (App)
    participant BL as Bootloader
    participant FL as Flash Memory

    Note over PC, FW: Step 1: Request Update
    PC->>FW: Send 0x510 "Enter Bootloader"
    FW->>FW: Write Magic Flag (RTC)
    FW->>FW: System Reset

    Note over PC, BL: Step 2: Connection
    BL->>BL: Read Magic Flag (It's set!)
    BL->>PC: Send 0x517 "Ping" (I am here)
    PC->>BL: Send 0x513 "Begin" (Size: 20KB)

    Note over PC, BL: Step 3: Transfer
    loop Every 256 Bytes
        PC->>BL: Send Data Chunks (0x520)
        BL->>FL: Write to Address 0x08008000
    end

    Note over PC, BL: Step 4: Finalize
    PC->>BL: Send 0x514 "End" (CRC Check)
    alt CRC OK
        BL->>FL: Clear Magic Flag
        BL->>BL: Reset -> Jump to App
    else CRC Fail
        BL->>PC: Send Error (0x51B)
        BL->>BL: Stay in Bootloader
    end
```

---

## 5. Troubleshooting Logic
**Goal:** Diagnose common field issues.

```mermaid
graph TD
    Start[Issue: No Data in UI] --> LED{Status LED Blinking?}
    
    LED -->|No| Power[Check Power 3.3V]
    LED -->|Yes| CAN{Check CAN Bus}
    
    CAN -->|PCanView Empty| Term[Check 120Ω Resistors]
    CAN -->|PCanView Shows Error| Baud[Check Baud Rate 250k]
    CAN -->|PCanView Shows Data| UI[Check UI Settings]
    
    UI -->|Wrong Adapter| Sel[Select Correct Adapter]
    UI -->|Wrong Mode| Mode[Check Weight/Brake Mode]
```
