# ATS Two-Wheeler UI User Guide

## 1. Overview
The ATS Two-Wheeler UI is a powerful real-time dashboard for measuring weight and brake force. It handles high-speed data from the STM32 via CAN bus and provides advanced tools for calibration and logging.

- **Developer Note**: Built with C# WPF (MVVM). Handles up to 1kHz CAN data streams with persistent logic for calibration and diagnostic monitoring.

---

## 2. Dashboard Features

### 2.1 Weight Display
The dashboard displays the total weight from all four load cells combined. 
- **Display Units**: You can switch between **kg** (Kilograms) and **N** (Newtons) in the settings.
- **Precision**: You can adjust the number of decimal places (0.0 vs 0.00) in the settings.

### 2.2 Brake & Peak Mode
Brake mode is used to measure the high-force "bite" of a vehicle's brakes.
- **Peak detection**: In Brake Mode, the system automatically catches the **highest force** achieved during the test and freezes it on the screen so you don't miss it.
- **Reset**: Click the Peak value to reset it to zero for the next test.

---

## 3. Technical Data Structures

The application processes raw sensor packets into user-friendly weight values using these internal models:

| Class | Property | Type | Description |
| :--- | :--- | :--- | :--- |
| **RawWeightData** | `RawADC` | `int` | Combined 4-channel sum from STM32. |
| **ProcessedWeightData**| `CalibratedWeight` | `double` | Weight after linear mapping (`y=mx+c`). |
| | `TaredWeight` | `double` | Final display weight (Calibrated + Tare). |
| | `TareValue` | `double` | The stored offset value. |

| Enum | Value | Description |
| :--- | :--- | :--- |
| **AdcMode** | `0` (Internal) | Standard 12-bit mode. |
| | `1` (ADS1115) | High-precision 16-bit mode. |
| **SystemMode** | `0` (Weight) | Relay OFF (Axle Weight). |
| | `1` (Brake) | Relay ON (Brake Sensors). |

---

## 4. Signal Filtering (Stop the Jitters)

Filters help stabilize the numbers on the screen so they don't "vibrate" or jump around due to mechanical noise.

- **EMA (Exponential)**: **Fast response**. It reacts quickly to changes, like a car's suspension smoothing out bumps while still letting you turn fast. Use this for live weighing.
- **SMA (Simple Average)**: **Slow and steady**. It averages the last few readings to give a rock-solid number. Use this for static weighing.

| Method | Type | Technical Logic | Best Used For... |
| :--- | :--- | :--- | :--- |
| **EMA** | Exponential | `Y = (Alpha * X) + (1 - Alpha) * Y_prev` | Live viewing and dynamic tests. |
| **SMA** | Simple | `Y = Sum(X_last_N) / N` | Final static weight verification. |

---

## 5. Calibration & Tare (How it Measures)

### 5.1 Calibration Modes
- **Regression**: Drawings a **straight line** through your points. Best for all standard sensors as it ignores minor measurement errors.
- **Piecewise**: **Connects the dots**. Only use this if your sensor is "non-linear" (meaning it behaves differently at light vs heavy weights).

| Mode | Logic | Technical Use Case |
| :--- | :--- | :--- |
| **Regression** | `y = mx + c` | Linear best-fit for entire range. |
| **Piecewise** | Linear Interpolation| Direct mapping between specific points. |

### 5.2 Tare Logic
Tare is designed to zero the scale, typically used after removing a heavy calibrator.
- **Persistence**: Tare is **saved to disk**. If you tare the scale and restart the app, it will still be at zero.
- **Math**: If the scale reads `-20kg`, clicking **Tare** stores `+20kg`. `Display = Reading + StoredOffset`.

---

## 6. System Health Monitoring (Auto-Check)

The app watches the connection to make sure the hardware is still talking.

- **Auto-Query**: If the system doesn't hear anything for 2 seconds, it automatically asks "Are you there?" (Status Request).
- **Unavailable**: If there is no answer for 5 seconds, the status light turns **Red**.

| Condition | Threshold | Action | UI Indication |
| :--- | :--- | :--- | :--- |
| **Idle** | > 2 Seconds | Sends `RequestStatus` (ID 0x032) | Status Icon: Orange |
| **No Response** | > 5 Seconds | Marks system as "Unavailable" | Status Icon: **Red** |

---

## 7. App Configuration Reference

| Setting Category | Key Parameter | Default | Technical Note |
| :--- | :--- | :--- | :--- |
| **Filtering** | `FilterAlpha` | `0.15` | Sensitivity of the EMA filter. |
| | `FilterWindowSize`| `10` | Samples used in the SMA average. |
| **Units** | `BrakeDisplayUnit` | `kg` | Can be switched to `N` (Newtons). |
| | `Multiplier` | `9.80665` | Gravity constant used for conversion. |
| **Logging** | `LogFileFormat` | `CSV` | Supports CSV (Excel) and JSON. |
| **Performance** | `BatchSize` | `50` | Stream throughput optimization. |

---

## 8. Technical Troubleshooting
- **Settings Store**: Located in `app_settings.json` in the Data folder.
- **CAN Protocol**: Operation fixed at **250 kbps**.
- **Log Location**: Stored in `/Data/Logs/` with timestamping.
- **Calibration**: Profiles saved per-mode as `calibration_internal.json` and `calibration_ads.json`.
