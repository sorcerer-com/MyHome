#ifndef MULTISENSOR_H
#define MULTISENSOR_H

//#define EMON

#ifndef EMON // MultiSense
#include "src/libs/DHT.h"
#else // EMON
#include "src/libs/EmonLib.h"
#endif

#include "src/RFNetwork.h"
#include "Settings.h"

class MultiSensor
{
  private:
    const int receiverPin = 2;
    const int transmitterPin = 3;
#ifndef EMON // MultiSense
    const int motionSensorPin = 4;
    const int tempHumSensorPin = 5;
    const int gasSensorPin = A0;
    const int lightingSensorPin = A1;
    const int ledPins[3] = { 9, 10, 11 };

    DHT dht;
    ulong LEDTimer = 0;
    bool prevMotion = false;
#else // EMON
    // TODO: multiple sensors?
    const int currentSensorPin = 4;

    const int voltage = 230; // V
    EnergyMonitor emon;
    ulong deltaTimer = 0;
    ulong counter = 0;
    ullong totalPower = 0;
#endif

    Settings settings;
    RFNetwork network;

  public:
    MultiSensor() :
      network(receiverPin, transmitterPin, 300)
#ifndef EMON // MultiSense
      , dht(tempHumSensorPin, DHT11)
    {
      LEDTimer = -settings.getLedONDuration() - 1;
    }
#else // EMON
    {
      emon.current(2, 29); // Cur Const = Ratio/BurdenR. 1800/62 = 29.
      deltaTimer = millis();
    }
#endif

    void begin() const
    {
#ifndef EMON // MultiSense
      dht.begin();
      pinMode(motionSensorPin, INPUT);
      pinMode(gasSensorPin, INPUT);
      pinMode(lightingSensorPin, INPUT);

      for (int i = 0; i < 3; i++)
        pinMode(ledPins[i], OUTPUT);
#endif

      randomSeed(analogRead(7));
      pinMode(LED_BUILTIN, OUTPUT);

      // if it is already created to network
      if (settings.getNetworkId() != 0 && settings.getNodeId() != 0)
      {
        network.setNetwork(settings.getNetworkId(), settings.getNodeId());
        DEBUG("// NetworkId: ");
        DEBUG(network.getNetworkId());
        DEBUG(", NodeId: ");
        DEBUGLN(network.getNodeId());
      }
    }


    void update()
    {
      checkNetwork();

#ifndef EMON // MultiSense
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
#else // EMON
      float power = emon.calcIrms(1480) * voltage; // 230 V
      if (power < 0.05 * voltage) // noise
        power = 0.0;
      totalPower += round(power);
      counter++;

      ulong delta = millis() - deltaTimer;
      if (delta > 20 * min)
      {
        ulong powerConsumption = getPowerConsumption();
        settings.setPowerConsumption(powerConsumption);
      }
      delay(200);
#endif
    }

  private:
    void checkNetwork() const
    {
      network.update();

      if (network.getNetworkId() == 0 && millis() > 20 * sec && millis() % (10 * sec) < sec) // if not connected after then 20th second try to connect every 10 sec
      {
        network.connect();
        settings.setNetworkId(network.getNetworkId());
        settings.setNodeId(network.getNodeId());
      }

      if (!network.ready())
        return;

      byte data[MAX_PACKAGE_SIZE];
      const byte len = network.recvPackage(data);
      data[len] = 0;

      if (strcmp(data, "disconnect") == 0) // disconnect from network
        disconnect(false);
      else if (strcmp(data, "getdata") == 0) // receive getdata command from the master node
      {
#ifndef EMON // MultiSense
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

        network.send(buff, 26);
#else // EMON
        float power = emon.calcIrms(1480) * voltage;
        if (power < 0.05 * voltage) // noise
          power = 0.0;
        ulong powerConsumption = getPowerConsumption();

        byte buff[14]; // 'data'(4) + '\0'(1) + power(4) + '\0'(1) + powerConsumption(4)
        strcpy(buff, "data");
        buff[4] = 0;
        float_to_bytes(power, &buff[5]);
        buff[9] = 0;
        buff[10]  = (byte)(powerConsumption >> 24);
        buff[11]  = (byte)(powerConsumption >> 16);
        buff[12]  = (byte)(powerConsumption >> 8);
        buff[13] = (byte)powerConsumption;

        network.send(buff, 9);
#endif
      }
      else if (strcmp(data, "data") == 0 && network.getNetworkId() == 0) // only master node
      {
        if (len == 26) // MultiSense data
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
        else if (len == 14) // Emon data
        {
          float power;
          bytes_to_float(&data[5], power);
          ulong powerConsumption = ((ulong)data[10] << 24) + ((ulong)data[11] << 16) + ((ulong)data[12] << 8) + data[13];
          getData(power, powerConsumption);
        }
      }
    }

#ifndef EMON // MultiSense
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
#else // EMON
    ulong getPowerConsumption() // returns power consumtions in watts per 0.01 hour
    {
      if (counter == 0)
        return 0;

      ulong delta = millis() - deltaTimer;
      // totalPower / ((3600 * 1000) / avgDelta)
      ullong power_hour = totalPower * delta / counter / 3600;
      power_hour = round(power_hour / (float)10);
      power_hour += settings.getPowerConsumption();

      deltaTimer = millis();
      counter = 0;
      totalPower = 0;
      settings.setPowerConsumption(0);
      return power_hour;
    }
#endif


  public:
    void setSetting(const char name[], const char value[]) const
    {
      settings.set(name, value);
    }

    void connect() const
    {
      if (network.getNetworkId() == 0) // if we aren't connected yet
      {
        // TODO:
        //network.createNetwork();
        network.setNetwork(1234, 1);
        settings.setNetworkId(network.getNetworkId());
        settings.setNodeId(network.getNodeId());
      }
      Serial.println(network.getNodeId());
      Serial.println("connected");
      DEBUG("// Network: ");
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
#ifndef EMON // MultiSense
      bool motion = digitalRead(motionSensorPin) == HIGH;
      float temperature = dht.readTemperature();
      float humidity = dht.readHumidity();
      float gasValue = (float)analogRead(gasSensorPin) / 1024;
      float lighting = (float)analogRead(lightingSensorPin) / 1024;
      lighting = 1.0f - lighting;

      getData(motion, temperature, humidity, gasValue, lighting);
#else // EMON
      float power = emon.calcIrms(1480) * voltage;
      if (power < 0.05 * voltage) // noise
        power = 0.0;
      ulong powerConsumption = getPowerConsumption();
      getData(power, powerConsumption);
#endif

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

    void getData(float currentPower, ulong powerConsumption) const
    {
      // send data
      Serial.println(network.getNodeId());
      Serial.println("data");

      DEBUGLN("// Current Power");
      Serial.println(currentPower);
      DEBUGLN("// Power Consumption");
      Serial.print(powerConsumption / 100);
      Serial.print(".");
      Serial.print((powerConsumption / 10) % 10);
      Serial.println(powerConsumption % 10);
    }
};

#endif // MULTISENSOR_H


