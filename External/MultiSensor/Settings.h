#ifndef SETTINGS_H
#define SETTINGS_H

#include <EEPROM.h>

#define ulong unsigned long

#define sec 1000

#define DEBUG

#ifdef DEBUG
#define DEBUG(...) Serial.print(__VA_ARGS__);
#define DEBUGLN(...) Serial.println(__VA_ARGS__);
#else
#define DEBUG(...)
#define DEBUGLN(...)
#endif

class Settings
{
private:
    const struct
    {
        const int begin = 0x00;
        const int ledLightingThreshold = begin;                         // float (4 bytes)
        const int ledONDuration = ledLightingThreshold + sizeof(float); // int  (2 bytes)
        const int ledColor = ledONDuration + sizeof(int);               // 3 * byte (3 bytes)
    } address;

    float ledLightingThreshold = 0.15f;
    int ledONDuration = 30 * sec;
    byte ledColor[3] = {255, 255, 255};

public:
    Settings()
    {
        // ledLightingThreshold
        if (EEPROM[address.ledLightingThreshold] != 0xFF)
            EEPROM.get(address.ledLightingThreshold, ledLightingThreshold);
        // ledOnDuration
        if (EEPROM[address.ledONDuration] != 0xFF)
            EEPROM.get(address.ledONDuration, ledONDuration);
        // ledColor
        if (EEPROM[address.ledColor] != 0xFF)
            EEPROM.get(address.ledColor, ledColor);
    }

    void set(const char name[], const char value[])
    {
        // NetworkId and NodeId should be able to be set
        if (strcmp(name, "LedLightingThreshold") == 0)
            setLedLightingThreshold((float)atof(value));
        else if (strcmp(name, "LedONDuration") == 0)
            setLedONDuration(atoi(value));
        else if (strcmp(name, "LedColor") == 0)
        {
            ulong value = (ulong)atol(value);
            setLedColor((byte)value, (byte)(value >> 8), (byte)(value >> 16));
        }
    }

    // LedLightingThreshold
    float getLedLightingThreshold()
    {
        return ledLightingThreshold;
    }

    void setLedLightingThreshold(const float &value)
    {
        ledLightingThreshold = value;
        EEPROM.put(address.ledLightingThreshold, ledLightingThreshold);
    }

    // LedONDuration
    int getLedONDuration()
    {
        return ledONDuration;
    }

    void setLedONDuration(const int &value)
    {
        ledONDuration = value;
        EEPROM.put(address.ledONDuration, ledONDuration);
    }

    // LedColor
    void getLedColor(byte &r, byte &g, byte &b)
    {
        r = ledColor[0];
        g = ledColor[1];
        b = ledColor[2];
    }

    void setLedColor(const byte &r, const byte &g, const byte &b)
    {
        ledColor[0] = r;
        ledColor[1] = g;
        ledColor[2] = b;
        EEPROM.put(address.ledColor, ledColor);
    }
};

#endif // SETTINGS_H
