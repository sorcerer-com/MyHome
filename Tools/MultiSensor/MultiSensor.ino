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

int nodeID = -1;
unsigned long timer = 0;
bool prevMotion = false;
DHT dht(tempHumSensorPin, DHT11);

void setup()
{
  Serial.begin(9600);

  dht.begin();
  pinMode(motionSensorPin, INPUT);
  pinMode(gasSensorPin, INPUT);
  pinMode(lightingSensorPin, INPUT);
}

void loop()
{
  if (millis() - timer < 100) // wait 100 ms
    return;
  timer = millis();
  
  if (Serial.available())
  {
    String command = Serial.readString();
    DEBUG_PRINTLN("//recevied: " + command);
    command.toLowerCase();
    if (command == "connect")
      connect();
    else if (command == "getdata")
      getData();
    return; // don't send motion with the command respond
  }

  bool motion = digitalRead(motionSensorPin) == HIGH;
  if (motion && !prevMotion)
  {
    Serial.println(nodeID);
    Serial.println("motion");
  }
  prevMotion = motion;
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
