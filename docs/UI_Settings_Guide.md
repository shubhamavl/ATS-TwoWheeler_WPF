# UI Settings & Configuration Guide

## Overview
This document explains every setting in the ATS Two-Wheeler PC Application. It includes **examples** and **logic** to help you choose the right values.

---

## 1. Connection & Connectivity

| Setting | Adapter Type |
| :--- | :--- |
| **PCAN-USB** | Uses the industry-standard `PCANBasic.dll`. Best for reliability. |
| **USB-CAN-A** | Uses generic Serial Port (`COMx`). Cheaper, but requires manual COM port selection. |
| **Simulator** | Connects to the internal software simulator. No hardware required. |

---

## 2. Weight Filtering (The Math Behind Smoothness)
Raw sensor data is always noisy. We use software filters to smooth it out.

### 2.1 Filter Type: SMA vs. EMA

#### **SMA (Simple Moving Average)**
*   **Logic**: Calculates the arithmetic average of the last $N$ samples. All samples have equal weight.
*   **Behavior**: Very stable, but "lazy". It takes time to catch up to sudden changes.
*   **Example**:
    *   Data Stream: `[10, 10, 10, 20, 20]` (Sudden jump to 20)
    *   Window 5: Average is `14`. The output slowly ramps up.
*   **Best For**: **Weight Mode** (Static weighing).

#### **EMA (Exponential Moving Average)**
*   **Logic**: A weighted average where **recent samples count more**.
    *   Formula: `Output = (Weight * NewInput) + ((1 - Weight) * OldOutput)`
*   **Behavior**: Reacts very fast to changes, but can "overshoot" or jitter if noise is high.
*   **Example**:
    *   Data Stream: `[10, 10, 10, 20, 20]`
    *   EMA reacts immediately to `20`, jumping to ~18 instantly.
*   **Best For**: **Brake Mode** (Fast transient forces).

### 2.2 SMA Window Size
*   **Definition**: How many past samples to keep in the buffer.
*   **Logic**:
    *   **Small Window (e.g., 5)**: fast response, more noise.
    *   **Large Window (e.g., 50)**: smooth line, slow response (lag).

**Real-World Example:**
| Window Size | Response Time | Visual Effect |
| :--- | :--- | :--- |
| **5 Samples** | ~5 ms | Line looks "jittery" like an earthquake. |
| **20 Samples** | ~20 ms | Smooth line, good balance. |
| **50 Samples** | ~50 ms | Line looks like slow-motion honey. |

---

## 3. Calibration Logic (Getting Accurate Numbers)

### 3.1 Method: Linear Regression
*   **Simple Calibration**: Uses just 2 points (Zero + 100kg). Draws a straight line.
*   **Linear Regression**: Can use **multiple** points (Zero, 20kg, 50kg, 100kg) to find the "Line of Best Fit". This reduces error if one measurement was slightly wrong.

### 3.2 Multi-Sample Averaging (The "Anti-Shake" Feature)
When you click "Calibrate", the rig might be vibrating. Taking 1 snapshot is risky.

*   **Logic**: We capture $N$ samples over $T$ milliseconds and calculate the "Truth".

#### **Mean vs. Median (Handling Outliers)**
Imagine you are calibrating with **100 kg**. But someone accidentally bumps the table for a split second.
*   **Data Captured**: `[100, 100, 100, 150 (Bump), 100]`

**Option A: Mean (Average)**
*   Calculation: `(100+100+100+150+100) / 5` = **110 kg**
*   Result: **WRONG**. The calibration is invalid because of the bump.

**Option B: Median (Middle Value)**
*   Sorted Data: `[100, 100, 100, 100, 150]`
*   Middle Value: **100 kg**
*   Result: **CORRECT**. The bump is ignored.
*   **Recommendation**: Always use **Median** in noisy environments (factories with vibrating machinery).

### 3.3 Outlier Removal (Sigma Clipping)
*   **Logic**: Automatically delete any capture that is "too far" from the average.
*   **Threshold**: Measured in Standard Deviations ($\sigma$).
    *   **2.0 $\sigma$**: Removes top/bottom 5% of extreme data.
    *   **Example**: If most data is `100 ± 1`, a value of `105` is deleted instantly before calculating the average.

---

## 4. Performance Settings (Tuning for Your PC)

### 4.1 Max Graph Points
*   **Definition**: How much history the graph remembers.
*   **Low (1,000)**: "Oscilloscope Mode". Data disappears off the left side quickly. CPU usage is low (1-2%).
*   **High (10,000)**: "History Mode". You see 10 seconds of data. CPU usage is high (10-15%).

### 4.2 Downsampling (Decimation)
*   **Problem**: We receive 1000 points/sec. The screen is only 1920 pixels wide. Drawing 10,000 points puts 5+ points on the *same pixel*. This wastes CPU.
*   **Solution**: "Draw every Nth point".
    *   **1:1**: Draw everything. Good for spotting micro-second spikes.
    *   **1:10**: Draw 1 out of 10 points. Visually identical for smooth waves. Saves 90% GPU power.

---

## 5. Brake Mode vs. Weight Mode Logic
The UI acts like two different machines depending on the mode.

### Scenario: You calibrated Weight Mode, but Brake Mode is wrong.
**Why?**
*   **Weight Mode** uses `weight_config.json` (Slope A).
*   **Brake Mode** uses `brake_config.json` (Slope B).
*   **Logic**: They are physically different sensors. Calibrating one does **not** fix the other. You must calibrate both separately.

### Unit Logic
*   **Weight Mode**: `kg` (Mass).
*   **Brake Mode**: `N` (Force).
*   **Conversion**: The UI logic is strictly linear calibration. It does not convert `kg * 9.8` to get Newtons. It relies on you physically calibrating with a Force Gauge (Newtons) for Brake Mode.

---

## 6. Troubleshooting Logic

**Q: The numbers are jumping around!**
1.  Check **Enable Weight Filtering** (Turn it ON).
2.  Switch to **SMA** (More stable than EMA).
3.  Increase **Window Size** to **20**.

**Q: The calibration is always off by 0.5 kg.**
1.  Enable **Multi-Sample Averaging**.
2.  Enable **Use Median** (Fixes "Bump" errors).
3.  Increase **Sample Count** to **100**.

**Q: The graph is lagging 1 second behind reality.**
1.  Your **Window Size** is too high (e.g., 100). Lower it to 10.
2.  Your **Max Graph Points** is too high (e.g., 50,000). Set to 5,000.
