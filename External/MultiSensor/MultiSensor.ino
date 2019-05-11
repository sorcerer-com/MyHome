#include "MultiSensor.h"

unsigned long timer = 0;
MultiSensor sensor;

void setup()
{
  Serial.begin(9600);
  DEBUGLN("// Setup");
  sensor.begin();
}

void loop()
{
  if (millis() - timer < 100) // wait 100 ms
    return;
  timer = millis();

  // check for received command
  if (Serial.available())
  {
    String command = Serial.readString();
    DEBUGLN("// Received: " + command);
    command.toLowerCase();

    if (command == "setsetting")
    {
      String name = Serial.readString();
      String value = Serial.readString();
      sensor.setSetting(name.c_str(), value.c_str());
    }
    else if (command == "getdata")
      sensor.getData();

    // TODO: add maybe indication using the LED
    return; // don't do anything than command respond
  }

  sensor.update();
}
