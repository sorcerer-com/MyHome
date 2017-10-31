#ifndef MULTISENSOR_H
#define MULTISENSOR_H

#include "src/DHT.h"

class MultiSensor {
  private:
    const int motionSensorPin = 2;
    const int tempHumSensorPin = 3;
    const int gasSensorPin = A0;
    const int lightingSensorPin = A1;
    const int LEDPins[3] = {9, 10, 11};

    DHT dht;

    ulong LEDtimer = 0;
    bool prevMotion = false;
  public:
    MultiSensor() : dht(tempHumSensorPin, DHT11) {
    }

    void begin() {
      dht.begin();
      pinMode(motionSensorPin, INPUT);
      pinMode(gasSensorPin, INPUT);
      pinMode(lightingSensorPin, INPUT);

      for (int i = 0; i < 3; i++)
        pinMode(LEDPins[i], OUTPUT);
    }

    void update() {
      // check for motion
      bool motion = digitalRead(motionSensorPin) == HIGH;
      if (motion && !prevMotion)
      {
        // TODO: nodeID?
        //Serial.println(nodeID);
        Serial.println(0);
        Serial.println("motion");
      }
      prevMotion = motion;

      // check the LED state
      updateLED(motion);
    }

    void updateLED(bool motion)
    {
      // TODO: to be a setting - ON time, LED max color, lighting threshold
      const int fadeTime = 3 * sec; // second
      const int fullTime = 30 * sec; // minute
      const float lightingThreshold = 0.2f;
      if (LEDtimer == 0) // on start
        LEDtimer = -fullTime - 1;
      ulong delta = millis() - LEDtimer;

      if (delta <= fadeTime) // LED fade in
      {
        float factor = (float)delta / fadeTime;
        // white
        analogWrite(LEDPins[0], 255 * factor);
        analogWrite(LEDPins[1], 255 * factor);
        analogWrite(LEDPins[2], 255 * factor);
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
        // white
        analogWrite(LEDPins[0], 255);
        analogWrite(LEDPins[1], 255);
        analogWrite(LEDPins[2], 255);
        //DEBUGLN("// LED fully ON");
      }
      else if (delta <= fullTime) // LED fade out
      {
        float factor = (float)(fullTime - delta) / fadeTime;
        // white
        analogWrite(LEDPins[0], 255 * factor);
        analogWrite(LEDPins[1], 255 * factor);
        analogWrite(LEDPins[2], 255 * factor);
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
        // white
        analogWrite(LEDPins[0], 0);
        analogWrite(LEDPins[1], 0);
        analogWrite(LEDPins[2], 0);
      }
    }


    void connect()
    {
      // TODO:
      //nodeID = 0; // master node
      //Serial.println(nodeID);
      Serial.println(0);
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
      // TODO: Serial.println(nodeID);
      Serial.println(0);
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
