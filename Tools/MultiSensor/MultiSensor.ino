#include "Utils.h"
#include "MultiSensor.h"

unsigned long timer = 0;
MultiSensor sensor;

void setup()
{
  Serial.begin(9600);
  DEBUGLN("//setup");
  sensor.begin();
}

void loop()
{
  if (millis() - timer < 100) // wait 100 ms
    return;
  timer = millis();

  // check for recevied command
  if (Serial.available())
  {
    String command = Serial.readString();
    DEBUGLN("//recevied: " + command);
    command.toLowerCase();

    if (command == "connect")
      sensor.connect();
    else if (command == "getdata")
      sensor.getData();
    return; // don't do anything than command respond
  }

  sensor.update();
}
