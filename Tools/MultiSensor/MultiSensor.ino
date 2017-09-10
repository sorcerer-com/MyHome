#include "DHT.h"

#define DEBUG

#ifdef DEBUG
  #define DEBUG_PRINT(...) { Serial.print(__VA_ARGS__); }
  #define DEBUG_PRINTLN(...) { Serial.println(__VA_ARGS__); }
#else
  #define DEBUG_PRINT(...) {}
  #define DEBUG_PRINTLN(...) {}
#endif

int motionSensorPin = 2;
int tempHumSensorPin = 3;
int gasSensorPin = A0;
int lightingSensorPin = A1;
int LEDPins[] = {9, 10, 11};

int nodeID = -1;
unsigned long timer = 0;
unsigned long LEDtimer = 0;
bool prevMotion = false;

DHT dht(tempHumSensorPin, DHT11);

void setup()
{
  Serial.begin(9600);

  dht.begin();
  pinMode(motionSensorPin, INPUT);
  pinMode(gasSensorPin, INPUT);
  pinMode(lightingSensorPin, INPUT);

  for (int i = 0; i < 3; i++)
    pinMode(LEDPins[i], OUTPUT);
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
    DEBUG_PRINTLN("//recevied: " + command);
    command.toLowerCase();

    if (command == "connect")
      connect();
    else if (command == "getdata")
      getData();
    return; // don't do anything than command respond
  }

  // check for motion
  bool motion = digitalRead(motionSensorPin) == HIGH;
  if (motion && !prevMotion)
  {
    Serial.println(nodeID);
    Serial.println("motion");
  }
  prevMotion = motion;

  // check the LED state
  checkLED(motion);
}


void connect()
{
  nodeID = 0; // master node
  Serial.println(nodeID);
  Serial.println("connected");
}

void getData()
{
  bool motion = digitalRead(motionSensorPin) == HIGH;
  float temperature = dht.readTemperature();
  float humidity = dht.readHumidity();
  float gasValue = (float)analogRead(gasSensorPin) / 1024;
  float lighting = (float)analogRead(lightingSensorPin) / 1024;
  lighting = 1.0f - lighting;

  // send data
  Serial.println(nodeID);
  Serial.println("data");

  DEBUG_PRINTLN("// Motion");
  Serial.println(motion);

  DEBUG_PRINTLN("// Tempreture(C)");
  Serial.println(temperature);
  DEBUG_PRINTLN("// Humidity(%)");
  Serial.println(humidity);

  DEBUG_PRINTLN("// Gas value");
  Serial.println(gasValue);

  DEBUG_PRINTLN("// Lighting value");
  Serial.println(lighting);
}


void checkLED(bool motion)
{
  // TODO: to be a setting - ON time, LED max color, lighting threshold
  const int fadeTime = 3000; // second
  const int fullTime = 30 * 1000; // minute
  if (LEDtimer == 0) // on start
	  LEDtimer = -fullTime - 1;
  unsigned long delta = millis() - LEDtimer;

  if (delta <= fadeTime) // LED fade in
  {
    float factor = 1.0f - ((float)(fadeTime - delta) / fadeTime);
    // white
    analogWrite(LEDPins[0], 255 * factor);
    analogWrite(LEDPins[1], 255 * factor);
    analogWrite(LEDPins[2], 255 * factor);
    //DEBUG_PRINT("// LED fade in ");
    //DEBUG_PRINTLN(factor);
  }
  else if (delta <= fullTime - fadeTime) // LED fully ON
  {
    if (motion)
      LEDtimer = millis() - fadeTime; // go in fully ON time
    // white
    analogWrite(LEDPins[0], 255);
    analogWrite(LEDPins[1], 255);
    analogWrite(LEDPins[2], 255);
    //DEBUG_PRINTLN("// LED fully ON");
  }
  else if (delta <= fullTime) // LED fade out
  {
    float factor = (float)(fullTime - delta) / fadeTime;
    // white
    analogWrite(LEDPins[0], 255 * factor);
    analogWrite(LEDPins[1], 255 * factor);
    analogWrite(LEDPins[2], 255 * factor);
    //DEBUG_PRINT("// LED fade out ");
    //DEBUG_PRINTLN(factor);
  }
  else // the LED should be fully OFF
  {
    LEDtimer = millis() - fullTime - 1; // this will fix problem when the millis() pass the unsigned long max value
    if (motion)
    {
      float lighting = (float)analogRead(lightingSensorPin) / 1024;
      lighting = 1.0f - lighting;
      if (lighting < 0.3f) // the lighting is low
      {
        LEDtimer = millis(); // start LED fade in
        DEBUG_PRINTLN("// Turn On LED");
      }
    }
  }
}
