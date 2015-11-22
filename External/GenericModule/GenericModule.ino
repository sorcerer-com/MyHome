#include <ESP8266WiFi.h>
#include <ESP8266WiFiMulti.h>
#include <ESP8266HTTPUpdateServer.h>
#include <ESP8266WebServer.h>
#include <ESP8266mDNS.h>

#define DEBUG //REMOTE_DEBUG
#define SF(str) String(F(str))

#define MILLIS_IN_A_SECOND 1000
#define SECONDS_IN_AN_HOUR 3600

#include "src/RemoteDebugger.h"
#include "Module.h"

ESP8266WiFiMulti wifiMulti;
ESP8266WebServer server(80);
ESP8266HTTPUpdateServer httpUpdater;

Module module(server);

unsigned long reconnectTimer = millis() - 5 * MILLIS_IN_A_SECOND;

// TODO: read 'press' value from MyHome
void setup()
{
  Serial.begin(9600);
#ifdef REMOTE_DEBUG
  RemoteDebugger.begin(server);
#endif

  module.data.readEEPROM();

  // setup WiFi
  WiFi.persistent(false);
  WiFi.mode(WIFI_STA);
  WiFi.hostname(module.name);

  wifiMulti.addAP(module.data.settings.wifi_ssid, module.data.settings.wifi_passphrase);

  // Wait for connection
  DEBUGLOG("GenericModule", "Connecting...");
  for (int i = 0; i < 10; i++)
  {
    if (wifiMulti.run() == WL_CONNECTED)
    {
      DEBUGLOG("GenericModule", "WiFi: %s, IP: %s", WiFi.SSID().c_str(), WiFi.localIP().toString().c_str());
      break;
    }
    delay(500);
  }

  if (MDNS.begin("module"))
  {
    DEBUGLOG("GenericModule", "MDNS responder started");
    //MDNS.addService("http", "tcp", 80); // block server when relay is connected
  }
  
  //ask server to track these headers
  const char *headerkeys[] = {"Cookie"};
  size_t headerkeyssize = sizeof(headerkeys) / sizeof(char *);
  server.collectHeaders(headerkeys, headerkeyssize);
  server.begin();

  httpUpdater.setup(&server, "admin", "admin");

  module.setup();
}

void loop()
{
  module.update();

  MDNS.update();
  server.handleClient();

  // Try to reconnect to WiFi
  if (millis() - reconnectTimer > 15 * MILLIS_IN_A_SECOND)
  {
    reconnectTimer = millis();
    if (wifiMulti.run() != WL_CONNECTED)
    {
      DEBUGLOG("GenericModule", "Fail to reconnect...");
      if (WiFi.getMode() == WIFI_STA)
      {
        // 192.168.4.1
        DEBUGLOG("GenericModule", "Create AP");
        WiFi.mode(WIFI_AP_STA);

        WiFi.softAP(module.name, "12345678");
        DEBUGLOG("GenericModule", "AP WiFi: %s, IP: %s", WiFi.softAPSSID().c_str(), WiFi.softAPIP().toString().c_str());
      }
    }
    else if (WiFi.getMode() != WIFI_STA)
    {
      WiFi.mode(WIFI_STA);
      DEBUGLOG("GenericModule", "Reconnected WiFi: %s, IP: %s", WiFi.SSID().c_str(), WiFi.localIP().toString().c_str());
    }
  }
}
