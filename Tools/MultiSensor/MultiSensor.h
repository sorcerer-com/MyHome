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
    const int receiverPin = 4;
    const int transmitterPin = 5;

    DHT dht;
    ulong LEDtimer = 0;
    bool prevMotion = false;

    Settings settings;
    RFNetwork network;

  public:
    MultiSensor() : dht(tempHumSensorPin, DHT11), network(receiverPin, transmitterPin, 300)
    {
      LEDtimer = -settings.getLedONDuration() - 1;
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
      checkNetwork();

      // check for motion
      bool motion = digitalRead(motionSensorPin) == HIGH;
      if (motion && !prevMotion)
      {
        Serial.println(network.getNodeId());
        Serial.println("motion");
      }
      prevMotion = motion;

      // check the LED state
      updateLED(motion);
    }

  private:
    void checkNetwork() const
    {
      network.update();

      if (network.getNetworkId() == 0 && millis() > 20 * sec && millis() % (10 * sec) < sec) // if not connected after then 20th second try to connect every 10 sec
        network.connect();

      if (!network.ready())
        return;

      byte data[MAX_PACKAGE_SIZE];
      const byte len = network.recvPackage(data);

      if (strcmp(data, "disconnect") == 0) // disconnect from network
        disconnect(false);
      else if (strcmp(data, "getdata") == 0) // receive getdata command from the master node
      {
        bool motion = digitalRead(motionSensorPin) == HIGH; // 1 byte
        float temperature = dht.readTemperature(); // 4 bytes
        float humidity = dht.readHumidity(); // 4 bytes
        float gasValue = (float)analogRead(gasSensorPin) / 1024; // 4 bytes
        float lighting = (float)analogRead(lightingSensorPin) / 1024; // 4 bytes
        lighting = 1.0f - lighting;

        byte buff[26]; // 'data'(4) + '\0'(1) + motion(1) + '\0'(1) + temperature(4) + '\0'(1) + humidity(4) + '\0'(1) + gasValue(4) + '\0'(1) + lighting(4)
        strcpy(buff, "data");
        buff[4] = 0;
        buff[5] = motion;
        buff[6] = 0;
        float_to_bytes(temperature, &buff[7]);
        buff[11] = 0;
        float_to_bytes(humidity, &buff[12]);
        buff[16] = 0;
        float_to_bytes(gasValue, &buff[17]);
        buff[21] = 0;
        float_to_bytes(lighting, &buff[22]);
      }
      else if (strcmp(data, "data") == 0 && network.getNetworkId() == 0) // only master node
      {
        bool motion = data[5];
        float temperature;
        bytes_to_float(&data[7], temperature);
        float humidity;
        bytes_to_float(&data[12], humidity);
        float gasValue;
        bytes_to_float(&data[17], gasValue);
        float lighting;
        bytes_to_float(&data[22], lighting);

        getData(motion, temperature, humidity, gasValue, lighting);
      }
    }

    void updateLED(const bool& motion)
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


  public:
    void setSetting(const char name[], const char value[]) const
    {
      settings.set(name, value);
    }

    void connect() const
    {
      // if network is already created
      if (settings.getNetworkId() != 0 && settings.getNodeId() != 0)
        network.setNetwork(settings.getNetworkId(), settings.getNodeId());
      else
      {
        // TODO:
        //network.createNetwork();
        network.setNetwork(1234, 1);
        settings.setNetworkId(network.getNetworkId());
        settings.setNodeId(network.getNodeId());
      }
      Serial.println(network.getNodeId());
      Serial.println("connected");
      DEBUG("// Created Network: ");
      DEBUGLN(network.getNetworkId());
    }

    void disconnect(bool send = true) const
    {
      if (send)
        network.print("disconnect");

      network.setNetwork(0, 0);
      settings.setNetworkId(0);
      settings.setNodeId(0);
    }

    void discover() const
    {
      network.discover();
    }

    void getData(bool send = true) const
    {
      bool motion = digitalRead(motionSensorPin) == HIGH;
      float temperature = dht.readTemperature();
      float humidity = dht.readHumidity();
      float gasValue = (float)analogRead(gasSensorPin) / 1024;
      float lighting = (float)analogRead(lightingSensorPin) / 1024;
      lighting = 1.0f - lighting;

      getData(motion, temperature, humidity, gasValue, lighting);

      if (send)
        network.print("getdata");
    }

  private:
    void getData(const bool& motion, const float& temperature, const float& humidity, const float& gasValue, const float& lighting) const
    {
      // send data
      Serial.println(network.getNodeId());
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


