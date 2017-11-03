#ifndef RFTRANSMITTER_H_
#define RFTRANSMITTER_H_

#if ARDUINO >= 100
#include "Arduino.h"
#else
#include "WProgram.h"
#include "pins_arduino.h"
#endif

class RFTransmitter {
    const byte outputPin;
    // Pulse lenght in microseconds
    const unsigned int pulseLength;
    // Backoff period for repeated sends in milliseconds
    unsigned int backoffDelay;
    // How often a reliable package is resent
    byte resendCount;
    byte lineState;

    void send0() {
      lineState = !lineState;
      digitalWrite(outputPin, lineState);
      delayMicroseconds(pulseLength << 1);
    }

    void send1() {
      digitalWrite(outputPin, !lineState);
      delayMicroseconds(pulseLength);
      digitalWrite(outputPin, lineState);
      delayMicroseconds(pulseLength);
    }

    void send00() {
      send0();
      delayMicroseconds(pulseLength << 1);
    }
    void send01() {
      send1();
      delayMicroseconds(pulseLength << 1);
    }
    void send10() {
      send1();
      send0();
    }
    void send11() {
      send1();
      send1();
    }

    void sendByte(byte data) {
      byte i = 4;
      do {
        switch(data & 3) {
        case 0:
          send00();
          break;
        case 1:
          send01();
          break;
        case 2:
          send10();
          break;
        case 3:
          send11();
          break;
        }
        data >>= 2;
      } while(--i);
    }

    void sendByteRed(byte data) {
      sendByte(data);
      sendByte(data);
      sendByte(data);
    }

    void sendPackage(byte *data, byte len) {
      // Synchronize receiver
      sendByte(0x00);
      sendByte(0x00);
      sendByte(0xE0);
    
      // Add crc to the message
      byte packageLen = len + 2;
      sendByteRed(packageLen);
    
      uint16_t crc = 0xffff;
      crc = crc_update(crc, packageLen);
    
      for (byte i = 0; i < len; ++i) {
        sendByteRed(data[i]);
        crc = crc_update(crc, data[i]);
      }
    
      sendByteRed(crc & 0xFF);
      sendByteRed(crc >> 8);
    
      digitalWrite(outputPin, LOW);
      lineState = LOW;
    }

  public:
    RFTransmitter(byte outputPin, unsigned int pulseLength = 100, unsigned int backoffDelay = 100, byte resendCount = 1) : 
        outputPin(outputPin), pulseLength(pulseLength), backoffDelay(backoffDelay), resendCount(resendCount) {

      pinMode(outputPin, OUTPUT);
      digitalWrite(outputPin, LOW);
      lineState = LOW;
    }

    void setBackoffDelay(unsigned int millies) {
      backoffDelay = millies;
    }
    
    void setResendCount(byte count) {
      resendCount = count;
    }
    
    void send(byte *data, byte len) {
      if (len > MAX_PAYLOAD_SIZE)
        len = MAX_PAYLOAD_SIZE;
    
      sendPackage(data, len);
    
      for (byte i = 0; i < resendCount; ++i) {
        delay(random(backoffDelay, backoffDelay << 1));
        sendPackage(data, len);
      }
    }
    
    void print(char *message) { 
      send((byte *)message, strlen(message));
    }
};

#endif /* RFTRANSMITTER_H_ */
