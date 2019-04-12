#ifndef MODULE_H
#define MODULE_H

#include <ESP8266WebServer.h>
#include <ESP8266HTTPClient.h>
#include <Hash.h>

#include "src/NTPClient.h"
#include "Data.h"

class Module
{
private:
    const char *gpio_names[GPIOS_COUNT] = {"D0", "D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8"};
    const int gpio_pins[GPIOS_COUNT] = {D0, D1, D2, D3, D4, D5, D6, D7, D8};

    uint32_t start_time;
    int gpio_states[GPIOS_COUNT];

    ESP8266WebServer &server;

public:
    String name;
    Data data;

    Module(ESP8266WebServer &server) : server(server)
    {
        start_time = 0;
        name = SF("Module_") + String(ESP.getChipId(), HEX);
    }

    void setup()
    {
        for (int i = 0; i < GPIOS_COUNT; i++)
        {
            if (data.settings.gpio_mode[i] == INPUT_MODE)
            {
                pinMode(gpio_pins[i], INPUT);
                digitalWrite(gpio_pins[i], LOW);
            }
            else if (data.settings.gpio_mode[i] == INPUT_PULLUP_MODE)
            {
                pinMode(gpio_pins[i], INPUT_PULLUP);
                digitalWrite(gpio_pins[i], HIGH);
            }
            else if (data.settings.gpio_mode[i] == OUTPUT_MODE)
            {
                pinMode(gpio_pins[i], OUTPUT);
                digitalWrite(gpio_pins[i], LOW);
            }
            gpio_states[i] = digitalRead(gpio_pins[i]);
        }

        server.on("/", HTTP_GET, [&]() { handleUI(); });
        server.on("/login", HTTP_ANY, [&]() { handleLogin(); });
        server.on("/settings", HTTP_POST, [&]() { handleSettings(); });
        server.on("/button", HTTP_GET, [&]() { handleButton(); });
        server.on("/reset", HTTP_GET, [&]() { handleReset(); });
        server.on("/restart", HTTP_GET, [&]() { handleRestart(); });

        // retry 5 times to get the time, else try every minute on update
        for (int i = 0; i < 5; i++)
        {
            if (setCurrentTime(getTime()))
                break;
        }

        date_time dt = getCurrentTime();
        DEBUGLOG("Module", "Start time: %s (%d)", dateTimeToString(dt).c_str(),
                 start_time + data.settings.time_zone * SECONDS_IN_AN_HOUR + (millis() / MILLIS_IN_A_SECOND));
    }

    unsigned long update_timer = millis();
    void update()
    {
        // if time isn't received in the setup, try again every 15th second
        if (start_time == 0 && (millis() / MILLIS_IN_A_SECOND) % 15 == 0)
            setCurrentTime(getTime());

        if (millis() - update_timer < 1000)
            return;

        for (int i = 0; i < GPIOS_COUNT; i++)
        {
            if (data.settings.gpio_mode[i] != INPUT_MODE)
                continue;

            int value = digitalRead(gpio_pins[i]);
            if (value != gpio_states[i])
            {
                gpio_states[i] = value;
                data.log(getCurrentTimeInSec(), i, value);
                sendValueRemote(gpio_names[i], value);
                // TODO: implement capability to work with different sensors - DHT, Gas, Temp/Hum, etc.
            }
        }
    }

    inline uint32_t getCurrentTimeInSec() const
    {
        uint32_t sTime = start_time != 0 ? start_time : data.settings.start_time;
        return sTime + data.settings.time_zone * SECONDS_IN_AN_HOUR + (millis() / MILLIS_IN_A_SECOND);
    }

    inline date_time getCurrentTime() const
    {
        return breakTime(getCurrentTimeInSec());
    }

    inline bool setCurrentTime(const uint32_t &value)
    {
        if (value == 0)
            return false;

        start_time = value;
        start_time -= millis() / MILLIS_IN_A_SECOND;
        data.settings.start_time = start_time;
        return true;
    }

private:
    String error;
    void sendValueRemote(const String &name, const int &value)
    {
        if (strlen(data.settings.remote_addess) == 0)
            return;

        HTTPClient http;
        http.setTimeout(1000);
        http.begin(data.settings.remote_addess);
        http.addHeader("token", data.settings.remote_token);
        http.addHeader("Content-Type", "application/json");
        String payload = SF("[{");
        payload += SF("\"name\": \"") + name + SF("\", ");
        payload += SF("\"value\": ") + String(value) + SF(", ");
        // TODO: add in settings
        payload += SF("\"aggrType\": \"avg\", ");
        payload += SF("\"desc\": \"Generic Module\"");
        payload += SF("}]");

        DEBUGLOG("Module", "Remote request to %s with payload '%s'", data.settings.remote_addess, payload.c_str());
        int httpCode = http.POST(payload);
        DEBUGLOG("Module", "Response code %d, content '%s'", httpCode, http.getString().c_str())
        if (httpCode != 200)
            error = http.errorToString(httpCode);
        else
            error = "";
        http.end();
    }

    void handleUI()
    {
        if (!authenticate())
            return;

        String result = SF("<html>");
        result += SF("<head>\n");
        result += SF("<title>Generic Module</title>\n");
        result += SF("<meta name='viewport' content='initial-scale=1.0, width=device-width'/>\n");
        result += SF("<meta http-equiv=\"refresh\" content=\"15;URL=/\">");
        result += SF("<meta charset='utf-8'>\n");
        result += SF("</head>\n");
        result += SF("<body>\n");
        result += SF("<h2>Generic Module</h2>\n");
        // GPIOs
        for (int i = 0; i < GPIOS_COUNT; i++)
        {
            if (data.settings.gpio_mode[i] == NONE_MODE)
                continue;

            result += gpio_names[i] + SF(" ");
            if (data.settings.gpio_mode[i] == OUTPUT_MODE) // if output add button
            {
                if (gpio_states[i] == HIGH)
                    result += SF("<a href='/button?") + gpio_names[i] + SF("=low'><button>LOW</button></a> ");
                else
                    result += SF("<a href='/button?") + gpio_names[i] + SF("=high'><button>HIGH</button></a> ");
                result += SF("<a href='/button?") + gpio_names[i] + SF("=press'><button>Press</button></a> ");
            }
            if (gpio_states[i] == HIGH)
                result += SF("HIGH");
            else
                result += SF("LOW");
            result += SF("<br/>\n");
        }
        result += SF("<br/>\n");
        if (error != "")
            result += SF("<font color='red'>Remote error: ") + error + SF("</font><br/>\n");
        // Logs
        result += SF("<h3>Log:</h3>\n");
        result += SF("<pre>\n");
        result += dateTimeToString(getCurrentTime()).c_str() + SF("\n");
        Data::LogEntry logs[LOGS_COUNT];
        int logs_count = data.get_logs(logs);
        for (int i = logs_count - 1; i >= 0; i--)
        {
            if (i == logs_count - 11)
                result += SF("<details><summary>More</summary>\n");

            result += dateTimeToString(breakTime(logs[i].time)) + SF("\t");
            result += gpio_names[logs[i].gpio_idx] + SF("\t");
            if (logs[i].value == HIGH)
                result += SF("HIGH\t");
            else
                result += SF("LOW\t");
            if (data.settings.gpio_mode[logs[i].gpio_idx] == INPUT_MODE ||
                data.settings.gpio_mode[logs[i].gpio_idx] == INPUT_PULLUP_MODE)
                result += "read";
            else if (data.settings.gpio_mode[logs[i].gpio_idx] == OUTPUT_MODE)
                result += "pressed";
            result += "\n";
        }
        if (logs_count > 10)
            result += SF("</details>");
        result += SF("</pre>\n");
        result += SF("<details open>\n");
        // Settings
        result += SF("<summary>Settings</summary>\n");
        result += SF("<br/>\n");
        result += SF("<form method='post' action='/settings'>\n");
        result += SF("Time Zone: <input type='number' name='time_zone' min='-11' max='12' value='") + data.settings.time_zone + SF("'/><br/>\n");
        result += SF("Password: <input type='password' name='password' maxlength='10' value='*****'/>\n");
        result += SF("<br/><br/>\n");
        // WiFi
        result += SF("WiFi<br/>\n");
        result += SF("SSID: <input type='text' name='wifi_ssid' value='") + data.settings.wifi_ssid + SF("'/>\n");
        result += SF("Passphrase: <input type='text' name='wifi_passphrase' value='") + data.settings.wifi_passphrase + SF("'/>\n");
        result += String(WiFi.RSSI()) + SF("\n");
        result += SF("<br/><br/>\n");
        // GPIOs
        result += SF("GPIOs<br/>\n");
        for (int i = 0; i < GPIOS_COUNT; i++)
        {
            result += gpio_names[i] + SF("\n");
            result += SF("<select name ='") + gpio_names[i] + SF("'>\n");
            if (data.settings.gpio_mode[i] == NONE_MODE)
                result += SF("<option value='none' selected>None</option>\n");
            else
                result += SF("<option value='none'>None</option>\n");
            if (data.settings.gpio_mode[i] == INPUT_MODE)
                result += SF("<option value='input' selected>Input</option>\n");
            else
                result += SF("<option value='input'>Input</option>\n");
            if (data.settings.gpio_mode[i] == INPUT_PULLUP_MODE)
                result += SF("<option value='input_pullup' selected>Input Pullup</option>\n");
            else
                result += SF("<option value='input_pullup'>Input Pullup</option>\n");
            if (data.settings.gpio_mode[i] == OUTPUT_MODE)
                result += SF("<option value='output' selected>Output</option>\n");
            else
                result += SF("<option value='output'>Output</option>\n");
            result += SF("</select>\n");
            result += SF("<br/>\n");
        }
        result += SF("<br/>\n");
        // Remote system
        result += SF("Remote system<br/>\n");
        result += SF("Address: <input type='text' name='remote_addess' value='") + data.settings.remote_addess + SF("'/>\n");
        result += SF("Token: <input type='text' name='remote_token' value='") + data.settings.remote_token + SF("'/>\n");
        result += SF("<br/><br/>\n");
        result += SF("<input type='submit'/>\n");
        result += SF("</form>\n");
        result += SF("<a href='/reset'><button>Reset settings</button></a>\n");
        result += SF("</details>\n");
        result += SF("</body>\n");
        result += SF("</html>\n");
        server.send(200, "text/html", result);
    }

    void handleLogin()
    {
        digitalWrite(LED_BUILTIN, LOW);
        if (server.method() == HTTP_GET)
        {
            String result = SF("<html>");
            result += SF("<head>\n");
            result += SF("<title>Generic Module</title>\n");
            result += SF("<meta name='viewport' content='initial-scale=1.0, width=device-width'/>\n");
            result += SF("<meta charset='utf-8'>\n");
            result += SF("</head>\n");
            result += SF("<body>\n");
            result += SF("<h2>Generic Module</h2>\n");
            result += SF("<form id='wifi_settings' action='/login' method='post'>\n");
            result += SF("<p>Password:</p>\n");
            result += SF("<input type='password' name='password' maxlength='10' autofocus/>\n");
            if (server.hasHeader("Cookie") && server.header("Cookie") == "login fail")
                result += SF("<p style='color:red'>Incorrect password</p>\n");
            else
                result += SF("<br/><br/>\n");
            result += SF("<input type='submit' value='LogIn'>\n");
            result += SF("</form>\n");
            result += SF("</body>\n");
            result += SF("</html>\n");
            server.send(200, "text/html", result);
        }
        else if (server.method() == HTTP_POST)
        {
            if (server.arg("password") == data.settings.password)
            {
                DEBUGLOG("Module", "Login");
                // set cookie with hash of remoteIp and password with max age 20 min
                String hash = sha1(server.client().remoteIP().toString() + data.settings.password);
                server.sendHeader("Set-Cookie", hash + ";Max-Age=1200;path=/");

                server.sendHeader("Location", "/", true);
                server.send(302, "text/plain", "");
            }
            else
            {
                DEBUGLOG("Module", "Wrong password");
                server.sendHeader("Set-Cookie", "login fail;Max-Age=3;path=/");
                server.sendHeader("Location", "/login", true);
                server.send(302, "text/plain", "");
            }
        }
        digitalWrite(LED_BUILTIN, HIGH);
    }

    void handleSettings()
    {
        if (!authenticate())
            return;

        for (int i = 0; i < server.args(); i++)
        {
            const String &name = server.argName(i);
            const String &value = server.arg(i);
            DEBUGLOG("Module", "Set setting %s: %s", name.c_str(), value.c_str());

            if (name == "time_zone")
                data.settings.time_zone = value.toInt();
            else if (name == "password" && value != "*****")
                strcpy(data.settings.password, value.c_str());
            else if (name == "wifi_ssid")
                strcpy(data.settings.wifi_ssid, value.c_str());
            else if (name == "wifi_passphrase")
                strcpy(data.settings.wifi_passphrase, value.c_str());
            else if (name == "remote_addess")
                strcpy(data.settings.remote_addess, value.c_str());
            else if (name == "remote_token")
                strcpy(data.settings.remote_token, value.c_str());
            else
            {
                for (int i = 0; i < GPIOS_COUNT; i++)
                {
                    if (name == gpio_names[i])
                    {
                        if (value == "none")
                            data.settings.gpio_mode[i] = NONE_MODE;
                        else if (value == "input")
                        {
                            data.settings.gpio_mode[i] = INPUT_MODE;
                            pinMode(gpio_pins[i], INPUT);
                            digitalWrite(gpio_pins[i], LOW);
                        }
                        else if (value == "input_pullup")
                        {
                            data.settings.gpio_mode[i] = INPUT_PULLUP_MODE;
                            pinMode(gpio_pins[i], INPUT_PULLUP);
                            digitalWrite(gpio_pins[i], HIGH);
                        }
                        else if (value == "output")
                        {
                            data.settings.gpio_mode[i] = OUTPUT_MODE;
                            pinMode(gpio_pins[i], OUTPUT);
                            digitalWrite(gpio_pins[i], LOW);
                        }
                        gpio_states[i] = digitalRead(gpio_pins[i]);
                        break;
                    }
                }
            }
        }
        data.writeEEPROM();

        // redirect to root
        server.sendHeader("Location", "/", true);
        server.send(302, "text/plain", "");
    }

    void handleButton()
    {
        if (!authenticate())
            return;

        const String &name = server.argName(0);
        const String &value = server.arg(0);
        DEBUGLOG("Module", "Button %s: %s", name.c_str(), value.c_str());
        for (int i = 0; i < GPIOS_COUNT; i++)
        {
            if (name == gpio_names[i])
            {
                if (value == "press")
                {
                    int v = (gpio_states[i] == HIGH ? LOW : HIGH); // set opposite state
                    digitalWrite(gpio_pins[i], v);
                    data.log(getCurrentTimeInSec(), i, v);
                    delay(100);
                    digitalWrite(gpio_pins[i], gpio_states[i]);
                    data.log(getCurrentTimeInSec(), i, gpio_states[i]);
                }
                else
                {
                    int v = (value == "high" ? HIGH : LOW);
                    digitalWrite(gpio_pins[i], v);
                    gpio_states[i] = v;
                    data.log(getCurrentTimeInSec(), i, v);
                }
                break;
            }
        }

        // redirect to root
        server.sendHeader("Location", "/", true);
        server.send(302, "text/plain", "");
    }

    void handleReset()
    {
        if (!authenticate())
            return;

        DEBUGLOG("Module", "Reset settings");
        data.reset();
        data.writeEEPROM();

        // restart
        server.client().setNoDelay(true);
        server.send(200, "text/html", F("<META http-equiv=\"refresh\" content=\"5;URL=/\">Rebooting...\n"));
        delay(100);
        server.client().stop();
        ESP.restart();
    }

    void handleRestart() const
    {
        if (!authenticate())
            return;

        DEBUGLOG("Module", "Restart");
        server.client().setNoDelay(true);
        server.send(200, "text/html", F("<META http-equiv=\"refresh\" content=\"5;URL=/\">Rebooting...\n"));
        delay(100);
        server.client().stop();
        ESP.restart();
    }

    bool authenticate(const bool &redirect = true) const
    {
        if (strcmp(data.settings.password, "") == 0) // empty password
            return true;

        bool res = false;
        if (server.hasHeader("Cookie"))
        {
            String hash = sha1(server.client().remoteIP().toString() + data.settings.password);
            res = (server.header("Cookie") == hash);
        }

        if (!res && redirect)
        {
            server.sendHeader("Location", "/login", true);
            server.send(302, "text/plain", "");
        }
        return res;
    }
};

#endif