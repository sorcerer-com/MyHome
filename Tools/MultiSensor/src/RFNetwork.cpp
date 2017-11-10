#include "RFNetwork.h"

RFNetwork::RFNetwork(byte inputPin, byte outputPin, uint pulseLength) :
  receiver(inputPin, pulseLength),
  transmitter(outputPin, pulseLength),
  nodeSendTime((((ulong)MAX_PACKAGE_SIZE * 8) * pulseLength) / 1000 / 2), // time to send the half(probability) package data in milliseconds
  fullSendTime(nodeSendTime * (MAX_SENDER_ID - 1)) // time all nodes to send data
{
  this->networkId = 0;
  this->nodeId = 0;
  this->packageId = 0;
  this->bufferLen = 0;

  for (int i = 0; i < MAX_SENDER_ID; i++)
    this->prevPackageIds[i] = 0;

  this->discoverTimer = millis();
  this->masterTimeOffset = 0;

  this->receiver.begin();
}


void RFNetwork::createNetwork()
{
  this->networkId = (uint)random((uint)(-1)); // "-1" = max unsigned int
  this->nodeId = 1;

  // broadcast ping with networkId
  this->print("ping");
  delay(100);

  for (int i = 0; i < (fullSendTime * 2) / 100; i++) // wait for two full send cycles
  {
    if (this->receiver.ready())
    {
      byte buff[MAX_PACKAGE_SIZE];
      const byte len = this->receiver.recvPackage(buff);
      uint networkId = ((uint)buff[0] << 8) + buff[1];
      if (networkId == this->networkId) // there is already such network with that id
      {
        this->createNetwork();
        break;
      }
    }
    digitalWrite(LED_BUILTIN, digitalRead(LED_BUILTIN) == LOW ? HIGH : LOW);
    delay(100);
  }
  digitalWrite(LED_BUILTIN, LOW);
}

void RFNetwork::setNetwork(const uint& networkId, const byte& nodeId)
{
  this->networkId = networkId;
  this->nodeId = nodeId;
}

void RFNetwork::discover(uint interval)
{
  if (this->nodeId != 1) // discover only the master node
    return;

  this->discoverTimer = millis() + interval;
}

void RFNetwork::connect()
{
  this->print("ping");
  delay(100);

  for (int i = 0; i < (fullSendTime * 2) / 100; i++) // wait for two full send cycles
  {
    if (this->receiver.ready()) // receive networkId and nodeId
    {
      byte buff[MAX_PACKAGE_SIZE];
      const byte len = this->receiver.recvPackage(buff);
      const char* data = (char*)(buff + 4);
      if (strcmp(data, "pong") == 0)
      {
        this->networkId = ((uint)buff[0] << 8) + buff[1];
        this->nodeId = data[5];
        ulong masterTime = ((ulong)buff[4 + 7] << 24) + ((ulong)buff[4 + 8] << 16) + ((ulong)buff[4 + 9] << 8) + buff[4 + 10];
        this->masterTimeOffset = masterTime - millis();
#ifdef DEBUG
        Serial.println("// Connected");
        Serial.print("// networkId: ");
        Serial.print(this->networkId);
        Serial.print(", nodeId: ");
        Serial.print(this->nodeId);
        Serial.print(", masterTime: ");
        Serial.println(masterTime);
        Serial.println();
#endif
        break;
      }
    }
    digitalWrite(LED_BUILTIN, digitalRead(LED_BUILTIN) == LOW ? HIGH : LOW);
    delay(100);
  }
  digitalWrite(LED_BUILTIN, LOW);
  delay(100);
}

void RFNetwork::update()
{
  if (millis() >= this->discoverTimer)
  {
    this->discoverTimer = millis(); // keep discoverTimer synced with millis
    digitalWrite(LED_BUILTIN, LOW);
  }
  else // led blink while in discover mode
    digitalWrite(LED_BUILTIN, digitalRead(LED_BUILTIN) == LOW ? HIGH : LOW);

  if (!this->receiver.ready())
    return;

  // read the package
  const byte len = this->receiver.recvPackage(this->buffer);
  this->buffer[len] = '\0';
  this->bufferLen = 0;

  if (this->networkId == 0) // drop the package if the node isn't connected
    return;

  if (((uint)this->buffer[0] << 8) + this->buffer[1] != 0) // received networkId != 0
    this->crypt(this->buffer, len);

  const uint networkId = ((uint)this->buffer[0] << 8) + this->buffer[1];
  const byte senderId = this->buffer[2];
  const byte packageId = this->buffer[3];
  const char* data = (char*)(this->buffer + 4);

#ifdef DEBUG
  Serial.println("// Receive Package");
  Serial.print("// data: ");
  for (int i = 0; i < len; i++)
  {
    Serial.print(this->buffer[i], HEX);
    Serial.print(" ");
  }
  Serial.println();

  Serial.print("// networkId: ");
  Serial.print(networkId);
  Serial.print(", senderId: ");
  Serial.print(senderId);
  Serial.print(", packageId: ");
  Serial.print(packageId);
  Serial.print(", ");
  Serial.println(data);
  Serial.println();
#endif

  // if we are in discover mode (only the "master" node)
  if (millis() < this->discoverTimer)
  {
    if (strcmp(data, "ping") == 0 && networkId == 0)
    {
      for (int i = 2; i < MAX_SENDER_ID; i++)
      {
        int idx = i * 2; // before the middle range - 4, 6, 8, ...
        if (i > MAX_SENDER_ID / 2) // if pass the middle - 3, 5, 7, ...
          idx = (i % (MAX_SENDER_ID / 2)) * 2 + 1;
        if (prevPackageIds[idx] == 0) // first free nodeId
        {
          char msg[11]; // 4("pong") + 1(0) + 1(NodeId) + 1(0) + 4(millis)
          strcpy(msg, "pong");
          msg[4] = 0;
          msg[5] = (char)idx;
          msg[6] = 0;
          const ulong time = millis();
          msg[7]  = (byte)(time >> 24);
          msg[8]  = (byte)(time >> 16);
          msg[9]  = (byte)(time >> 8);
          msg[10] = (byte)time;
#ifdef DEBUG
          Serial.println("// Add Node");
          Serial.print("// nodeId: ");
          Serial.print(idx);
          Serial.print(", masterTime: ");
          Serial.println(time);
          Serial.println();
#endif

          delay(100);
          this->send((byte*)msg, 11, false);
          break;
        }
      }
      return;
    }
  }

  // if the package is from the different network or the sender isn't valid
  if (networkId != this->networkId || senderId <= 0 || senderId > MAX_SENDER_ID)
    return;

  if (this->prevPackageIds[senderId] == packageId) // if the package is already received
    return;
  this->prevPackageIds[senderId] = packageId;

  if (strcmp(data, "ping") == 0) // if the command is ping, respond with pong
  {
    this->print("pong");
  }
  else if (strcmp(data, "pong") != 0) // add package to the buffer
  {
    this->bufferLen = len;

    // resend the package
    this->waitToSend();
    this->receiver.stop();
    this->crypt(this->buffer, this->bufferLen);
    this->transmitter.send(this->buffer, this->bufferLen);
    this->crypt(this->buffer, this->bufferLen);
    delay(100);
    this->receiver.begin();
  }
}


void RFNetwork::print(const char* message)
{
  this->send((byte*)message, strlen(message));
}

void RFNetwork::send(const byte* data, byte len, bool crypt = true)
{
  ++this->packageId;
  this->prevPackageIds[this->nodeId] = this->packageId;

  this->resend(data, len, crypt);
}

void RFNetwork::resend(const byte* data, byte len, bool crypt) const
{
  this->waitToSend();

  // package: networkId(2 byte) + senderId(1 byte) + packageId(1 byte) + data
  byte newData[MAX_PACKAGE_SIZE];
  newData[0] = (byte)(this->networkId >> 8);
  newData[1] = (byte)this->networkId;
  newData[2] = this->nodeId;
  newData[3] = this->packageId;
  memcpy(newData + 4, data, len);
  newData[len + 4] = '\0';

#ifdef DEBUG
  Serial.println("// Send Package");
  Serial.print("// data: ");
  for (int i = 0; i < len + 4; i++)
  {
    Serial.print(newData[i], HEX);
    Serial.print(" ");
  }
  Serial.print((char*)(newData + 4));
  Serial.println(crypt ? " 'crypted'" : "");
  Serial.println();
#endif

  if (crypt)
    this->crypt(newData, len + 4);

  this->receiver.stop();
  this->transmitter.send(newData, len + 4);
  delay(100);
  this->receiver.begin();
}

void RFNetwork::waitToSend() const
{
  if (this->nodeId <= 0)
    return;

  const ulong masterTime = millis() + this->masterTimeOffset;
  ulong nextSendWindow = (masterTime / fullSendTime) * fullSendTime + (this->nodeId - 1) * nodeSendTime; // calculate begin(for 0 node) master time and add id of the current node multiplied to send time
  if (masterTime > nextSendWindow) // if we already passed the send window for the current node, wait for full send time
    nextSendWindow += fullSendTime;

#ifdef DEBUG
  Serial.print("// wait: ");
  Serial.print(nextSendWindow - masterTime);
  Serial.print(" / ");
  Serial.print(fullSendTime);
  Serial.print(" (");
  Serial.print(nodeSendTime);
  Serial.println(")");
#endif
  delay(nextSendWindow - masterTime);
}

void RFNetwork::crypt(byte* data, byte len) const
{
  uint key = this->networkId;
  for (int i = 0; i < len; i++)
  {
    byte b = (byte)key;
    if (i % 2 == 1) b = (byte)(key >> 8);
    data[i] ^= b;
  }
}

bool RFNetwork::ready() const
{
  return this->bufferLen > 0;
}

byte RFNetwork::recvPackage(byte* data, byte* pSenderId, byte* pPackageId)
{
  if (!this->ready())
    return 0;

  //uint networkId = ((uint)this->buffer[0] << 8) + this->buffer[1];
  const byte senderId = this->buffer[2];
  const byte packageId = this->buffer[3];

  memcpy(data, this->buffer + 4, this->bufferLen - 4);
  if (pSenderId)
    *pSenderId = senderId;
  if (pPackageId)
    *pPackageId = packageId;

  const byte len = this->bufferLen - 4;
  this->bufferLen = 0;
  return len;
}


byte RFNetwork::getNodeId() const
{
  return this->nodeId;
}

uint RFNetwork::getNetworkId() const
{
  return this->networkId;
}

