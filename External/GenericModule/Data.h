#ifndef DATA_H
#define DATA_H

#include <EEPROM.h>

#define NONE_MODE 0
#define INPUT_MODE 1
#define INPUT_PULLUP_MODE 2
#define OUTPUT_MODE 3

#define GPIOS_COUNT 9
#define LOGS_COUNT 300

extern "C" uint32_t _SPIFFS_end;

class Data
{
public:
    struct Settings
    {
        uint32_t start_time = 0;
        int8_t time_zone = 0;
        char password[10];
        // WiFi
        char wifi_ssid[32];
        char wifi_passphrase[64];
        // GPIOs
        uint8_t gpio_mode[GPIOS_COUNT];
        // Remote
        char remote_addess[50];
        char remote_token[33];
    } settings;

    struct LogEntry
    {
        uint32_t time = 0;
        uint8_t gpio_idx = 0;
        uint8_t value = 0;
    } logs[LOGS_COUNT];

    int next_log = 0;

    Data()
    {
        EEPROM.begin(4096);
    }

    inline void log(const uint32_t &time, const uint8_t &gpio_idx, const uint8_t &value)
    {
        logs[next_log].time = time;
        logs[next_log].gpio_idx = gpio_idx;
        logs[next_log].value = value;
        // write to EEPROM
        uint32_t addr = ((uint32_t)&_SPIFFS_end - 0x40200000); // EEPROM start address
        addr += sizeof(settings) + next_log * sizeof(LogEntry);
        DEBUGLOG("Data", "Write log %d to EEPROM in addr %d", next_log, addr);
        spi_flash_write(addr, reinterpret_cast<uint32_t *>(&logs[next_log]), sizeof(LogEntry));

        // set next one to 0xFF
        next_log = (next_log + 1) % LOGS_COUNT;
        memset(&logs[next_log], 0xFF, sizeof(LogEntry));
    }

    int get_logs(LogEntry result[LOGS_COUNT])
    {
        int idx = 0;
        for (int i = 0; i < LOGS_COUNT; i++)
        {
            int logs_idx = (next_log + i) % LOGS_COUNT;
            if (logs[logs_idx].gpio_idx != 0xFF)
            {
                result[idx] = logs[logs_idx];
                idx++;
            }
        }
        return idx;
    }

    void reset()
    {
        settings.start_time = 0;
        settings.time_zone = 0;
        strcpy(settings.password, "");
        strcpy(settings.wifi_ssid, "");
        strcpy(settings.wifi_passphrase, "");
        memset(settings.gpio_mode, 0, sizeof(settings.gpio_mode));
        strcpy(settings.remote_addess, "");
        strcpy(settings.remote_token, "");

        memset(logs, 0xFF, sizeof(logs));
    }

    void readEEPROM()
    {
        if (EEPROM.getDataPtr() == NULL) // if cannot read the EEPROM
            return;

        EEPROM.get(0, settings);
        EEPROM.get(sizeof(settings), logs);
        for (next_log = 0; next_log < LOGS_COUNT; next_log++)
        {
            if (logs[next_log].gpio_idx == 0xFF)
                break;
        }
        DEBUGLOG("Data", "Read settings with size: %d, next log: %d", sizeof(settings), next_log);
    }

    void writeEEPROM()
    {
        // clear the EEPROM data buffer first
        memset(EEPROM.getDataPtr(), 0xFF, EEPROM.length());

        DEBUGLOG("Data", "Write settings with size: %d, next log: %d", sizeof(settings), next_log);
        EEPROM.put(0, settings);
        EEPROM.put(sizeof(settings), logs);
        EEPROM.commit();
    }
};

#endif
