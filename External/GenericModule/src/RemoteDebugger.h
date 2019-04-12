#ifndef REMOTE_DEBUGGER_H
#define REMOTE_DEBUGGER_H

#include <StreamString.h>

#ifdef DEBUG
#define DEBUGLOG(category, ...)            \
    {                                      \
        printMillis(Serial, millis());     \
        Serial.printf(" %-15s", category); \
        Serial.printf(__VA_ARGS__);        \
        Serial.println();                  \
    }
#elif defined REMOTE_DEBUG
#define DEBUGLOG(category, ...)                    \
    {                                              \
        printMillis(RemoteDebugger, millis());     \
        RemoteDebugger.printf(" %-15s", category); \
        RemoteDebugger.printf(__VA_ARGS__);        \
        RemoteDebugger.println();                  \
        RemoteDebugger.clean();                    \
    }
#else
#define DEBUGLOG(category, ...)
#endif

void printMillis(Print &print, unsigned long value)
{
    value = value / 1000; // to seconds
    print.printf("%02d:%02d:%02d", value / 60 / 60, value / 60 % 60, value % 60);
}

#ifdef REMOTE_DEBUG
class RemoteDebuggerClass : public StreamString
{
  private:
    uint32_t m_maxSize;

  public:
    void begin(ESP8266WebServer &server, const uint32_t &maxSize = 0)
    {
        if (maxSize != 0)
            m_maxSize = maxSize;
        else
            m_maxSize = ESP.getFreeHeap() / 3;

        server.on("/debug", [&]() {
            String result = F("<META http-equiv=\"refresh\" content=\"5;URL=/debug\">\n");
            result += F("<link rel=\"icon\" href=\"data:;base64,iVBORw0KGgo=\">\n");
            result += F("<p style='white-space: pre;font-family: monospace;'>");
            result += String(length()) + F("/") + String(m_maxSize) + "\n";
            result += c_str();
            result += F("</p>");
            server.send(200, "text/html", result);
        });
    }

    void clean()
    {
        // if above 90%
        if (length() > m_maxSize * 9 / 10)
        {
            // remove at least the half string
            String temp = substring(indexOf('\n', length() / 2) + 1);
            copy(temp.c_str(), temp.length());
        }
    }
};

#if !defined(NO_GLOBAL_INSTANCES) && !defined(NO_GLOBAL_EEPROM)
RemoteDebuggerClass RemoteDebugger;
#endif

#endif

#endif