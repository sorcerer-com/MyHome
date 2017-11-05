#ifndef MULTISENSOR_H
#define MULTISENSOR_H

#include "src/libs/DHT.h"
#include "src/RFNetwork.h"
#include "Settings.h"

class MultiSensor
{
  private:
    const int motionSensorPin = 2;
    const int tempHumSensorPin = 3;
    const int gasSensorPin = A0;
    const int lightingSensorPin = A1;
    const int ledPins[3] = { 9, 10, 11 };
    const int receiverPin = 10;
    const int transmitterPin = 11;

    DHT dht;
    ulong LEDtimer = 0;
    bool prevMotion = false;

    Settings settings;
    RFNetwork net;

  public:
    MultiSensor() : dht(tempHumSensorPin, DHT11), net(receiverPin, transmitterPin, 300)
    {
        LEDtimer = -settings.getLedONDuration() - 1;
    }

    void begin()
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
      net.update();

      // check for motion
      bool motion = digitalRead(motionSensorPin) == HIGH;
      if (motion && !prevMotion)
      {
        Serial.println(net.getNodeId());
        Serial.println("motion");
      }
      prevMotion = motion;

      // check the LED state
      updateLED(motion);
    }

    void updateLED(bool motion)
    {
      const int fadeTime = 3 * sec; // second
      const int fullTime = settings.getLedONDuration();
      const float lightingThreshold = settings.getLedLightingThreshold();
      byte r, g, b;
      settings.getLedColor(r, g, b);
      
      ulong delta = millis() - LEDtimer;
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
            LEDtimer = millis() - fadeTime - 1; // go in fully ON time
          else
            LEDtimer = millis() - fullTime + fadeTime - 1; // go to fade out
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
        LEDtimer = millis() - fullTime - 1; // this will fix problem when the millis() pass the unsigned long max value
        if (motion)
        {
          float lighting = (float)analogRead(lightingSensorPin) / 1024;
          lighting = 1.0f - lighting;
          if (lighting < lightingThreshold) // the lighting is low
          {
            LEDtimer = millis(); // start LED fade in
            DEBUGLN("// Turn On LED");
          }
        }
        // turn off
        analogWrite(ledPins[0], 0);
        analogWrite(ledPins[1], 0);
        analogWrite(ledPins[2], 0);
      }
    }


    void connect()
    {
      // if network is already created
      if (settings.getNetworkId() != 0 && settings.getNodeId() != 0)
        net.setNetwork(settings.getNetworkId(), settings.getNodeId() != 0);
      else
      {
        // TODO:
        //net.createNetwork();
        settings.setNetworkId(net.getNetworkId());
        settings.setNodeId(net.getNodeId());
      }
      Serial.println(net.getNodeId());
      Serial.println("connected");
      DEBUG("// Created Network: ");
      DEBUGLN(net.getNetworkId());
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
      Serial.println(net.getNodeId());
      Serial.println("data");

      DEBUGLN("// Motion");
      Serial.println(motion);

      DEBUGLN("// Tempreture(C)");
      Serial.println(temperature);
      DEBUGLN("// Humidity(%)");
      Serial.println(humidity);

      DEBUGLN("// Gas value");
      Serial.println(gasValue);

      DEBUGLN("// Lighting value");
      Serial.println(lighting);
    }
};

#endif // MULTISENSOR_H
