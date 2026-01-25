# ATS Two-Wheeler System - FAQ

## Overview
This document answers frequently asked questions about the ATS Two-Wheeler Weight Measurement System, organized by component.

---

## 1. ADC (Analog-to-Digital Conversion)

### Q1.1: Can I run the ADS1115 at 1000 Hz like the Internal ADC?

**A:** No, it is physically impossible.

**Reason:**
- ADS1115 maximum data rate: **860 SPS** (Samples Per Second)
- Minimum conversion time: **~1.16 ms per sample**
- 1000 Hz requires **1.00 ms**, which exceeds hardware capability

**Practical Limits:**
- **Brake Mode (1 Channel)**: Max ~800 Hz
- **Weight Mode (4 Channels)**: Max ~215 Hz (860 SPS ÷ 4)

**Solution:** Use **Internal ADC Mode** for 1000 Hz sampling (12-bit, DMA-driven).

---

### Q1.2: Which ADC mode should I use for my application?

**A:** Choose based on your requirements:

| Requirement | Recommended Mode | Reason |
|:---|:---|:---|
| High speed (≥500 Hz) | Internal ADC | DMA-driven, zero CPU overhead |
| High precision (>12-bit) | ADS1115 | 16-bit resolution |
| Low noise | ADS1115 | Better SNR, differential inputs |
| Simple wiring | Internal ADC | Direct GPIO connection |
| Multiple sensors | ADS1115 | I2C bus supports multiple devices |

---

### Q1.3: Why do I see different data types for Internal vs ADS1115?

**A:** Data type differences reflect hardware characteristics:

- **Internal ADC**: `uint16_t` (unsigned, 0-4095 per channel)
  - Sum of 4 channels: 0-16380
- **ADS1115**: `int16_t` (signed, -32768 to +32767 per channel)
  - Sum of 4 channels: -131072 to +131068

**Reason:** ADS1115 supports differential measurements (can be negative), while Internal ADC is single-ended (always positive).

---

### Q1.4: What is the moving average filter and can I change it?

**A:** The moving average filter reduces noise by averaging the last N samples.

**Current Configuration:**
- **Filter Size**: 4 samples (`ADC_FILTER_SAMPLES`)
- **Type**: Simple Moving Average (SMA)
- **Per-Channel**: Each channel has independent filter

**To Change:**
1. Edit `adc_unified.c`: `#define ADC_FILTER_SAMPLES 8` (example: 8 samples)
2. Rebuild firmware
3. Trade-off: Larger N = smoother but slower response

---

### Q1.5: Why does the relay need 20ms settling time?

**A:** Mechanical relays exhibit contact bounce during switching.

**Physics:**
- Relay contacts physically move and can bounce 5-15 times
- Bounce duration: 1-20 ms depending on relay quality
- During bounce, voltage is unstable

**Solution:** Wait 20ms (`RELAY_SETTLING_TIME_MS`) before reading ADC to ensure stable signal.

---

## 2. CAN Protocol

### Q2.1: What CAN message IDs are used in the system?

**A:** See the complete list in [CAN Protocol Specification](file:///c:/Users/u32n08/git/ATS-TwoWheeler_WPF/docs/CAN_Protocol.md).

**Most Common:**
- **0x200**: Total Raw Data (Telemetry)
- **0x300**: System Status
- **0x302**: Performance Metrics
- **0x040**: Start Stream (Command)
- **0x044**: Stop Stream (Command)
- **0x050**: Set System Mode (Weight/Brake)

---

### Q2.2: Why is CAN transmission limited to 250 kbps?

**A:** 250 kbps is a deliberate choice for reliability.

**Reasons:**
1. **Cable Length**: Supports up to 250m cable runs (vs 40m @ 1 Mbps)
2. **EMI Immunity**: Lower frequency = better noise rejection
3. **Termination Tolerance**: More forgiving of imperfect termination
4. **Bandwidth**: Sufficient for 1 kHz data rate (8 bytes @ 1 kHz = 8 KB/s << 31.25 KB/s available)

**Can I increase it?** Yes, but requires:
- Shorter cables (<40m for 1 Mbps)
- Better shielding
- Proper 120Ω termination at both ends

---

### Q2.3: What does "Semantic IDs" mean in the CAN protocol?

**A:** Message type is encoded in the CAN ID itself, not in the payload.

**Traditional Approach:**
```
ID: 0x100 (Generic)
Payload: [0x01, data...] // 0x01 = "Status Message"
```

**Semantic ID Approach:**
```
ID: 0x300 (System Status)
Payload: [data...] // No type byte needed
```

**Benefits:**
- Smaller payload (1 byte saved per message)
- Faster filtering (hardware CAN filters)
- Self-documenting (ID tells you what it is)

---

### Q2.4: Why do I see "DLC=2" for Internal ADC but "DLC=4" for ADS1115?

**A:** Data Length Code (DLC) varies by ADC mode due to data type size.

**Internal ADC (12-bit):**
- Per-channel: 0-4095 (fits in 12 bits)
- Sum of 4: 0-16380 (fits in 16 bits = 2 bytes)
- **DLC = 2**

**ADS1115 (16-bit):**
- Per-channel: -32768 to +32767 (requires 16 bits)
- Sum of 4: -131072 to +131068 (requires 32 bits = 4 bytes)
- **DLC = 4**

---

### Q2.5: How do I troubleshoot CAN communication errors?

**A:** Follow this diagnostic sequence:

1. **Check Physical Layer:**
   - Verify 120Ω termination at both ends
   - Measure CAN_H and CAN_L voltage (should be ~2.5V idle)
   - Check cable continuity

2. **Check Software:**
   - Monitor `g_tx_count` (should increment)
   - Check `g_error_count` (should be 0)
   - View CAN messages in PCAN-View

3. **Common Issues:**
   - No termination → Bus-Off errors
   - Wrong baud rate → No messages received
   - Swapped CAN_H/CAN_L → Inverted data

---

## 3. STM32 Firmware

### Q3.1: What is the memory layout of the firmware?

**A:** Depends on build configuration:

**Standalone Build:**
```
0x08000000 - 0x0803FFFF: Application (256 KB)
```

**Bootloader Build:**
```
0x08000000 - 0x08007FFF: Bootloader (32 KB)
0x08008000 - 0x0803FFFF: Application (224 KB)
```

**To Check:** View `config.h`:
- `STANDALONE_BUILD` defined → Starts at 0x08000000
- `BOOTLOADER_BUILD` defined → Starts at 0x08008000

---

### Q3.2: Why does the firmware version show 0.2.0 but the bootloader shows 1.1?

**A:** Firmware and Bootloader are versioned independently.

**Reason:**
- **Firmware**: Application logic (changes frequently)
- **Bootloader**: Update mechanism (rarely changes)

**Version Scheme:**
- Firmware: `FW_VERSION_STRING` in `config.h`
- Bootloader: `BOOTLOADER_VERSION_MAJOR/MINOR` in `bootloader.h`

**Current Versions:**
- Firmware: v0.2.0
- Bootloader: v1.1

---

### Q3.3: What is the main loop frequency and can I change it?

**A:** Main loop runs at **1 kHz** (1 ms period).

**Controlled By:**
- `MAIN_LOOP_FREQ_HZ` in `config.h` (1000)
- `while(1)` loop in `main.c` uses `HAL_Delay(1)`

**Can I change it?** Yes, but:
- **Faster (>1 kHz)**: May cause timing issues with I2C/CAN
- **Slower (<1 kHz)**: Reduces responsiveness

**Recommendation:** Keep at 1 kHz unless you have specific requirements.

---

### Q3.4: How do I enable debug output via UART?

**A:** UART1 is configured but not actively used in production.

**To Enable:**
1. Uncomment `#define DEBUG` in `config.h`
2. Use `DEBUG_PRINT("Message\n")` in code
3. Connect UART1 (PA9=TX, PA10=RX) to USB-Serial adapter
4. Open terminal at 115200 baud

**Warning:** UART operations are blocking and will affect timing.

---

### Q3.5: What are "Live Expressions" and how do I use them?

**A:** Live Expressions allow real-time variable monitoring during debugging.

**Setup (STM32CubeIDE):**
1. Start debug session
2. Window → Show View → Live Expressions
3. Add variables:
   - `g_adc_sample_count`
   - `g_tx_count`
   - `g_ats_raw_data.total_raw`
   - `g_current_mode`

**Benefits:**
- No breakpoints needed
- Real-time updates
- Minimal performance impact

See [Debugging Guide](file:///c:/Users/u32n08/git/ATS-TwoWheeler_WPF/docs/Debugging_Guide.md) for details.

---

## 4. PC UI (WPF Application)

### Q4.1: Why does the UI show "Not Connected" even though the device is powered?

**A:** Check the following:

1. **CAN Adapter:**
   - PCAN-USB: Install PCAN-Basic driver
   - USB-CAN-A: Check COM port in Device Manager

2. **Connection Settings:**
   - UI → Settings → Select correct adapter
   - Verify baud rate: 250 kbps

3. **Firmware:**
   - Ensure firmware is running (LED blinking?)
   - Check CAN termination (120Ω resistors)

4. **Test:**
   - Open PCAN-View
   - Should see 0x300 (Status) messages at 1 Hz

---

### Q4.2: How do I calibrate the system?

**A:** Calibration is performed in the PC UI, not on the STM32.

**Procedure:**
1. **Tare (Zero):**
   - Remove all weight
   - Click "Tare" button
   - UI stores offset value

2. **Calibration:**
   - Place known weight (e.g., 100 kg)
   - Click "Calibrate"
   - Enter actual weight
   - UI calculates slope

3. **Verification:**
   - Place different known weights
   - Verify displayed values

**Storage:** Calibration data saved in `settings.json` (per ADC mode).

---

### Q4.3: Why does the graph show gaps or missing data?

**A:** Possible causes:

1. **CAN Bus Overload:**
   - Reduce streaming rate (1 kHz → 500 Hz)
   - Check `g_tx_count` vs expected rate

2. **UI Performance:**
   - Close other applications
   - Reduce graph history (Settings → Max Points)

3. **USB Latency:**
   - Use USB 2.0 port (not USB 3.0 hub)
   - Check USB-CAN adapter firmware

---

### Q4.4: Can I log data to a file?

**A:** Yes, the UI has built-in data logging.

**To Enable:**
1. UI → Settings → Enable Logging
2. Select log directory
3. Start streaming
4. Data saved as CSV with timestamp

**Log Format:**
```csv
Timestamp,Mode,RawValue,CalibratedWeight
2026-01-25 19:00:00.123,Weight,12345,567.8
```

---

### Q4.5: How do I update the firmware via the UI?

**A:** Use the Bootloader Manager.

**Steps:**
1. UI → Settings → Bootloader Manager
2. Click "Enter Bootloader" (device resets)
3. Wait for "Ready" beacon (0x517 messages)
4. Select `.bin` file
5. Click "Start Update"
6. Wait ~30 seconds
7. Device auto-resets to application

**Troubleshooting:**
- No beacon → Check bootloader version (must be v1.0+)
- Update fails → Verify `.bin` file is for correct build (Bootloadable, not Standalone)

---

## 5. Simulator

### Q5.1: What does the Simulator do?

**A:** The Simulator emulates the STM32 firmware for UI testing without hardware.

**Features:**
- Generates realistic ADC patterns (sine, ramp, noise)
- Responds to CAN commands (start/stop stream, mode switch)
- Simulates Weight and Brake modes
- No physical hardware required

**Use Cases:**
- UI development
- Algorithm testing
- Demo/training

---

### Q5.2: How do I run the Simulator?

**A:** 

1. **Install Virtual CAN:**
   - Download PCAN-USB driver (includes virtual CAN)
   - Or use SocketCAN (Linux)

2. **Run Simulator:**
   ```
   cd ATS_TwoWheeler_Simulator
   dotnet run
   ```

3. **Connect UI:**
   - UI → Settings → Select Virtual CAN
   - Click Connect

---

### Q5.3: Can the Simulator inject faults for testing?

**A:** Yes, the Simulator supports fault injection.

**Available Faults:**
- Random noise (configurable amplitude)
- Dropout (missing messages)
- Offset drift
- Sensor saturation

**Configuration:** Edit `simulator_config.json`

---

## 6. Bootloader

### Q6.1: What is the difference between Standalone and Bootloadable firmware?

**A:** 

| Aspect | Standalone | Bootloadable |
|:---|:---|:---|
| **Start Address** | 0x08000000 | 0x08008000 |
| **Size** | 256 KB | 224 KB |
| **Update Method** | ST-Link only | CAN Bootloader |
| **Use Case** | Factory programming | Field updates |

**File Naming:**
- Standalone: `ATS_FW_vX.X.X.bin`
- Bootloadable: `ATS_FW_vX.X.X_BL.bin`

---

### Q6.2: How do I recover from a failed bootloader update?

**A:** Use ST-Link to reflash.

**Recovery Procedure:**
1. Connect ST-Link to SWD header (SWDIO, SWCLK, GND, 3.3V)
2. Open STM32CubeProgrammer
3. Connect to device
4. Erase chip (if necessary)
5. Flash bootloader: `Bootloader_vX.X.X.bin` @ 0x08000000
6. Flash application: `ATS_FW_vX.X.X_BL.bin` @ 0x08008000
7. Disconnect ST-Link
8. Power cycle device

---

### Q6.3: Why does the bootloader send 0x517 messages every 500ms?

**A:** Proactive Ping for auto-detection.

**Purpose:**
- UI can detect bootloader mode without user action
- Enables "Auto-Update" feature
- Confirms bootloader is alive and ready

**Message:** `0x517` (Ping Response), DLC=0, every 500ms

**To Disable:** Not recommended, but edit `BOOTLOADER_PING_INTERVAL_MS` in `bootloader.c`

---

### Q6.4: What happens if I flash the wrong firmware binary?

**A:** Depends on the mismatch:

| Scenario | Result | Recovery |
|:---|:---|:---|
| Standalone → Bootloadable slot | Won't boot (wrong address) | Reflash with ST-Link |
| Bootloadable → Standalone slot | Boots, but no bootloader | Flash bootloader separately |
| Wrong MCU (e.g., F103 → F302) | Won't boot | Reflash correct binary |
| Corrupted binary | CRC fail, stays in bootloader | Reflash via CAN |

**Prevention:** Always verify binary filename before flashing.

---

### Q6.5: Can I update the bootloader itself?

**A:** Yes, but requires ST-Link (cannot self-update via CAN).

**Reason:** Bootloader cannot overwrite itself while running.

**Procedure:**
1. Connect ST-Link
2. Flash new bootloader @ 0x08000000
3. Verify with STM32CubeProgrammer
4. Disconnect ST-Link

**Warning:** Failed bootloader update bricks the device (requires ST-Link recovery).

---

## 7. System Integration

### Q7.1: What is the recommended startup sequence?

**A:** 

1. **Power On** (3.3V stable)
2. **Bootloader Check** (RTC backup register)
   - If magic flag set → Stay in bootloader
   - Else → Jump to application
3. **Application Init:**
   - ADC calibration (Internal mode)
   - CAN initialization
   - Timer start (1 kHz)
4. **Main Loop** (1 kHz)

**Total Boot Time:** ~200 ms (bootloader) + ~50 ms (app init) = **~250 ms**

---

### Q7.2: How do I switch between Weight and Brake modes?

**A:** Via CAN command or button.

**CAN Command:**
```
ID: 0x050
Data: [0x00] // Weight Mode
Data: [0x01] // Brake Mode
```

**Button:** Press Button 1 (PB1) on STM32 board

**Sequence:**
1. Send 0x050 command
2. Firmware switches relay (PB12)
3. Wait 20ms (relay settling)
4. Firmware resets ADC filters
5. Firmware updates `g_system_mode`

---

### Q7.3: What are the power requirements?

**A:** 

- **Voltage:** 3.3V ±5% (3.135V - 3.465V)
- **Current:**
  - Idle: ~50 mA
  - Active (1 kHz sampling): ~80 mA
  - Peak (relay switching): ~150 mA
- **Recommended PSU:** 5V 1A USB → 3.3V LDO regulator

**Load Cells:**
- Excitation: 3.3V (from STM32)
- Current: ~10 mA per cell × 4 = 40 mA

---

### Q7.4: Can I use this system with a different microcontroller?

**A:** Yes, but requires porting.

**Minimum Requirements:**
- **ADC**: 12-bit, 4 channels, DMA support
- **CAN**: 2.0B, 250 kbps
- **I2C**: 400 kHz (for ADS1115)
- **Timer**: 1 kHz interrupt
- **Flash**: 64 KB (application only)
- **RAM**: 16 KB

**Recommended MCUs:**
- STM32F103 (Cortex-M3)
- STM32F4xx (Cortex-M4)
- STM32G0xx (Cortex-M0+)

**Porting Effort:** ~2-3 days (HAL abstraction layer)

---

