#ifndef RFNETWORK_H
#define RFNETWORK_H

#include "Utils.h"
#include "RFReceiver.h"
#include "RFTransmitter.h"

const int MAX_SENDER_ID = 31;

class RFNetwork
{  
  private:
    const RFReceiver receiver;
    const RFTransmitter transmitter;
    const ulong nodeSendTime;
    const ulong fullSendTime;
    
    uint networkId;
    byte nodeId;
    byte packageId;
    byte buffer[MAX_PACKAGE_SIZE];
    byte bufferLen;

    // Used to filter out duplicate packages
    byte prevPackageIds[MAX_SENDER_ID + 1];

    ulong discoverTimer;
    long masterTimeOffset;

    void waitToSend() const;
    void crypt(byte* data, byte len) const;

  public:
    RFNetwork(byte inputPin, byte outputPin, uint pulseLength = 100);

    void createNetwork();
	void setNetwork(const uint& networkId, const byte& nodeId);
    void discover(uint interval);
    void connect();
    void update();

    void print(const char *message);
    void send(const byte* data, byte len, bool crypt = true);
    void resend(const byte* data, byte len, bool crypt = true) const;
    bool ready() const;
    byte recvPackage(byte* data, byte* pSenderId = 0, byte* pPackageId = 0);

    uint getNetworkId() const;
    byte getNodeId() const;
};

#endif // RFNETWORK_H
