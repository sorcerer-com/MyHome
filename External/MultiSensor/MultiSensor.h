#ifndef MULTISENSOR_H
#define MULTISENSOR_H

#include "src/DHT.h"

#include "Settings.h"

class MultiSensor
{
  private:
    const int motionSensorPin = 4;
    const int tempHumSensorPin = 5;
    const int gasSensorPin = A0;
    const int lightingSensorPin = A1;
    const int ledPins[3] = { 9, 10, 11 };

    DHT dht;
    ulong LEDTimer = 0;
    bool prevMotion = false;

    Settings settings;

  public:
    MultiSensor() : dht(tempHumSensorPin, DHT11)
    {
      LEDTimer = -settings.getLedONDuration() - 1;
    }

    void begin() const
    {
      dht.begin();
      pinMode(motionSensorPin, INPUT);
      pinMode(gasSensorPin, INPUT);
      pinMode(lightingSensorPin, INPUT);

      for (int i = 0; i < 3; i++)
        pinMode(ledPins[i], OUTPUT);

      randomSeed(analogRead(7));
      pinMode(LED_BUILTIN, OUTPUT);
    }


    void update()
    {
      // check for motion
      bool motion = digitalRead(motionSensorPin) == HIGH;
      if (motion && !prevMotion)
      {
        Serial.println("[{'name': 'Motion', 'value': true, 'aggrType': 'avg', 'desc': 'Motion detection'}]");
      }
      prevMotion = motion;

      // check the LED state
      updateLED(motion);
    }

  private:
    void updateLED(const bool& motion)
    {
      const int fadeTime = 3 * sec; // second
      const int fullTime = settings.getLedONDuration();
      const float lightingThreshold = settings.getLedLightingThreshold();
      byte r, g, b;
      settings.getLedColor(r, g, b);

      ulong delta = millis() - LEDTimer;
      if (delta <= fadeTime) // LED fade in
      {
        float factor = (float)delta / fadeTime;
        analogWrite(ledPins[0], r * factor);
        analogWrite(ledPins[1], g * factor);
        analogWrite(ledPins[2], b * factor);
        //DEBUG("// LED fade in ");
        //DEBUGLN(factor);
      }
      else if (delta <= fullTime - fadeTime) // LED fully ON
      {
        if (motion)
        {
          float lighting = (float)analogRead(lightingSensorPin) / 1024;
          lighting = 1.0f - lighting;
          if (lighting < lightingThreshold) // the lighting is low
            LEDTimer = millis() - fadeTime - 1; // go in fully ON time
          else
            LEDTimer = millis() - fullTime + fadeTime - 1; // go to fade out
        }
        analogWrite(ledPins[0], r);
        analogWrite(ledPins[1], g);
        analogWrite(ledPins[2], b);
        //DEBUGLN("// LED fully ON");
      }
      else if (delta <= fullTime) // LED fade out
      {
        float factor = (float)(fullTime - delta) / fadeTime;
        analogWrite(ledPins[0], r * factor);
        analogWrite(ledPins[1], g * factor);
        analogWrite(ledPins[2], b * factor);
        //DEBUG("// LED fade out ");
        //DEBUGLN(factor);
      }
      else // the LED should be fully OFF
      {
        LEDTimer = millis() - fullTime - 1; // this will fix problem when the millis() pass the unsigned long max value
        if (motion)
        {
          float lighting = (float)analogRead(lightingSensorPin) / 1024;
          lighting = 1.0f - lighting;
          if (lighting < lightingThreshold) // the lighting is low
          {
            LEDTimer = millis(); // start LED fade in
            DEBUGLN("// Turn On LED");
          }
        }
        // turn off
        analogWrite(ledPins[0], 0);
        analogWrite(ledPins[1], 0);
        analogWrite(ledPins[2], 0);
      }
    }

  public:
    void setSetting(const char name[], const char value[]) const
    {
      settings.set(name, value);
    }

    void getData(bool send = true) const
    {
      bool motion = digitalRead(motionSensorPin) == HIGH;
      float temperature = dht.readTemperature();
      float humidity = dht.readHumidity();
      float gasValue = (float)analogRead(gasSensorPin) / 1024;
      float lighting = (float)analogRead(lightingSensorPin) / 1024;
      lighting = 1.0f - lighting;

      // [
      //  {'name': 'Motion', 'value': False, 'aggrType': 'avg', 'desc': 'description'}, 
      //  {'name': 'Temperature', 'value': 114.3, 'aggrType': 'avg', 'desc': 'description'}, 
      //  {'name': 'Humidity', 'value': 93.4, 'aggrType': 'avg', 'desc': 'description'}, 
      //  {'name': 'Smoke', 'value': 37, 'aggrType': 'avg', 'desc': 'description'}, 
      //  {'name': 'Lighting', 'value': 82, 'aggrType': 'avg', 'desc': 'description'}
      // ]
      String result = "[";
      result += "{'name': 'Motion', 'value': " + String(motion ? "true" : "false") + ", 'aggrType': 'avg', 'desc': 'Motion detection'},";
      result += "{'name': 'Temperature', 'value': " + String(temperature) + ", 'aggrType': 'avg', 'desc': 'Current temperature'},";
      result += "{'name': 'Humidity', 'value': " + String((float)round(sqrt(humidity) * 10)) + ", 'aggrType': 'avg', 'desc': 'Current humidity'},";
      result += "{'name': 'Smoke', 'value': " + String((float)round(gasValue * 100)) + ", 'aggrType': 'avg', 'desc': 'Smoke detection'},";
      result += "{'name': 'Lighting', 'value': " + String((float)round(lighting * 100)) + ", 'aggrType': 'avg', 'desc': 'Current lighting'}";
      result += "]";

      Serial.println(result);
    }
};

#endif // MULTISENSOR_H
