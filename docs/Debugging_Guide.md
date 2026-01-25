# Debugging Guide

## 1. Live Expressions (STM32CubeIDE)
Add these variables to the "Live Expressions" view for real-time monitoring of the running firmware.

### System Health
| Variable | Type | Description | Expected Value |
| :--- | :--- | :--- | :--- |
| `g_system_uptime_ms` | `uint32_t` | System uptime in milliseconds | Increasing |
| `g_system_initialized` | `uint8_t` | Initialization status | 1 (Ready) |
| `g_last_error_code` | `enum` | Last reported error | 0 (ERROR_NONE) |
| `g_force_bootloader` | `uint8_t` | Boot entry flag | 0 (Normal) / 1 (Boot Req) |

### Performance & Telemetry
| Variable | Type | Description | Expected Value |
| :--- | :--- | :--- | :--- |
| `g_perf.can_tx_hz` | `uint16_t` | CAN transmission rate | ~1000 (at 1kHz) |
| `g_perf.adc_sample_hz` | `uint16_t` | ADC sampling rate | ~1000 (at 1kHz) |
| `g_tx_count` | `uint32_t` | Total CAN frames sent | Increasing Rapidly |
| `g_rx_count` | `uint32_t` | Total CAN frames received | Increasing (on commands) |
| `g_error_count` | `uint32_t` | CAN Error Counter | 0 (Ideally) |

### Measurement State
| Variable | Type | Description | Expected Value |
| :--- | :--- | :--- | :--- |
| `g_relay_state` | `uint8_t` | Current Relay state | 0 (Weight) / 1 (Brake) |
| `g_current_mode` | `enum` | Active ADC Backend (Internal/ADS) | 0 (Internal) / 1 (ADS1115) |
| `g_ats_raw_data.total_raw` | `int32_t` | Combined sensor value | varies (0-16380 or signed high) |
| `g_stream_enabled` | `uint8_t` | Is data streaming? | 1 (Yes) / 0 (No) |
| `g_stream_rate_sel` | `uint8_t` | Selected Rate ID | 3 (1kHz), 2 (500Hz), etc. |

## 2. Key Global Variables
*   **`hcan`**: CAN Handle. Check `hcan.Instance->ESR` for error counters (REC/TEC).
*   **`hadc1`**: Internal ADC Handle.
*   **`adc_dma_buffer[4]`**: Raw DMA buffer for Internal ADC.
*   **`g_ats_raw_data`**: The atomic data structure holding latest sensor readings.
*   **`g_adc_sample_count`**: Free-running counter of total ADC samples (useful to verify ADC ISR is running).
*   **`g_ads1115_read_pending`**: Flag for ADS1115 read scheduling (should toggle rapidly).

## 3. Common Error Codes
| Flag (Hex) | Meaning | Resolution |
| :--- | :--- | :--- |
| `0x01` | ADC Init Failed | Check hardware connections to sensors. |
| `0x02` | CAN Init Failed | Check CAN transceiver and termination (120Ω). |
| `0x04` | I2C Error | ADS1115 not responding (Check Pull-ups). |

## 4. Bootloader Debugging
If the bootloader is stuck:
1.  Check `RTC->BKP0R` register. It should be `0x00000000` in app mode.
2.  If `0xDEADC0DE`, it's forcing bootloader entry.
3.  Check `Bootloader_Jump` address. RAM stack pointer should be `0x200xxxxx`.
4.  Verify LED blinking (1Hz = Bootloader active).
