# Bootloader Documentation

## 1. Update Flow
The bootloader uses a "Magic Flag" mechanism to enter update mode safely.
1.  **Enter Request**:
    *   PC sends `0x510` (Enter Bootloader).
    *   App writes `0xDEADC0DE` to `RTC->BKP0R`.
    *   App performs System Reset.
2.  **Boot Entry**:
    *   System Startup checks `RTC->BKP0R`.
    *   If Magic matches, enters `Bootloader_Main` loop.
3.  **Discovery**:
    *   Bootloader broadcasts `0x517` (Ping Response) every 500ms.
    *   PC detects ping and knows device is ready.
4.  **Transfer**:
    *   PC sends `0x513` (Begin).
    *   PC streams firmware chunks via `0x520`.
    *   PC sends `0x514` (End).
5.  **Completion**:
    *   PC sends `0x515` (Reset).
    *   Bootloader clears Magic and Reset.
    *   System boots into new Application.

## 2. Protocol Specification (v0.1)
All Bootloader CAN IDs are in the `0x51x` range.

| ID | Name | Dir | Description |
| :--- | :--- | :--- | :--- |
| `0x510` | **Enter Bootloader** | RX | Request to reboot into bootloader. |
| `0x512` | **Ping** | RX | Check if bootloader is alive. |
| `0x517` | **Ping Response** | TX | "I am here" (Proactive). |
| `0x513` | **Begin Update** | RX | Start new firmware session. |
| `0x520` | **Data** | RX | Firmware binary data chunk. |
| `0x514` | **End Update** | RX | Verify CRC and finalize. |
| `0x515` | **Reset** | RX | Reboot system. |
| `0x51B` | **Error** | TX | Protocol error (Sequence, CRC, Size). |

## 3. Usage Guide (PC App)
To update firmware using the **ATS Two-Wheeler UI**:
1.  Go to **Settings** -> **Bootloader**.
2.  Click **Browse** and select the `.bin` file.
3.  Click **"Start Update"**.
    *   *Note: The system will automatically handle the reboot sequence.*
4.  Watch the progress bar.
5.  Wait for the "Update Complete" message.
