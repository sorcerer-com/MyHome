#ifndef RECEIVER_H
#define RECEIVER_H

#if ARDUINO >= 100
#include "Arduino.h"
#else
#include "WProgram.h"
#include "pins_arduino.h"
#endif

#include "libs/PinChangeInterruptHandler.h"

static inline byte recoverByte(const byte b1, const byte b2, const byte b3) {
  // Discard all bits that occur only once in the three input bytes
  // Use all bits that are in b1 and b2
  byte res = b1 & b2;
  // Use all bits that are in b1 and b3
  res |= b1 & b3;
  // Use all bits that are in b2 and b3
  res |= b2 & b3;
  return res;
}

enum {
  MAX_PAYLOAD_SIZE = 82,
  MIN_PACKAGE_SIZE = 2,
  MAX_PACKAGE_SIZE = MAX_PAYLOAD_SIZE + MIN_PACKAGE_SIZE,
};

class RFReceiver : PinChangeInterruptHandler {
    const byte inputPin;
    const unsigned int pulseLimit;

    // Input buffer and input state
    byte shiftByte;
    byte errorCorBuf[3];
    byte bitCount, byteCount, errorCorBufCount;
    unsigned long lastTimestamp;
    bool packageStarted;

    byte inputBuf[MAX_PACKAGE_SIZE];
    byte inputBufLen;
    uint16_t checksum;
    volatile bool inputBufReady;
    byte changeCount;

    byte recvDataRaw(byte * data) {
      while (!inputBufReady);

      byte len = inputBufLen;
      memcpy(data, inputBuf, len - 2);

      // Enable the input as fast as possible
      inputBufReady = false;
      // The last two bytes contain the checksum, which is no longer needed
      return len - 2;
    }

  public:
    RFReceiver(byte inputPin, unsigned int pulseLength = 100) : inputPin(inputPin),
      pulseLimit((pulseLength << 2) - (pulseLength >> 1)), shiftByte(0),
      bitCount(0), byteCount(0), errorCorBufCount(0), lastTimestamp(0),
      packageStarted(false), inputBufLen(0), checksum(0),
      inputBufReady(false), changeCount(0) {

    }
    void begin() {
      pinMode(inputPin, INPUT);
      attachPCInterrupt(digitalPinToPCINT(inputPin));
    }

    void stop() {
      detachPCInterrupt(digitalPinToPCINT(inputPin));
    }

    /*
       Returns true if a valid and deduplicated package is in the buffer, so
       that a subsequent call to recvPackage() will not block.

       @returns True if recvPackage() will not block
    */
    bool ready() const {
      return inputBufReady;
    }

    byte recvPackage(byte * data) {
      for (;;) {
        return recvDataRaw(data);
      }
    }

    void decodeByte(byte inputByte) {
      if (!packageStarted)
        return;

      errorCorBuf[errorCorBufCount++] = inputByte;

      if (errorCorBufCount != 3)
        return;
      errorCorBufCount = 0;

      if (!byteCount) {
        // Quickly decide if this is really a package or not
        if (errorCorBuf[0] < MIN_PACKAGE_SIZE || errorCorBuf[0] > MAX_PACKAGE_SIZE ||
            errorCorBuf[0] != errorCorBuf[1] || errorCorBuf[0] != errorCorBuf[2]) {
          packageStarted = false;
          return;
        }

        inputBufLen = errorCorBuf[0];
        checksum = crc_update(checksum, inputBufLen);
      } else {
        byte data = recoverByte(errorCorBuf[0], errorCorBuf[1], errorCorBuf[2]);
        inputBuf[byteCount - 1] = data;
        // Calculate the checksum on the fly
        checksum = crc_update(checksum, data);

        if (byteCount == inputBufLen) {
          // Check if the checksum is correct
          if (!checksum) {
            inputBufReady = true;
          }

          packageStarted = false;
          return;
        }
      }

      ++byteCount;
    }

    virtual void handlePCInterrupt(int8_t pcIntNum, bool value) {
      if (inputBufReady)
        return;

      ++changeCount;

      {
        unsigned long time = micros();
        if (time - lastTimestamp < pulseLimit)
          return;

        lastTimestamp = time;
      }

      shiftByte = (shiftByte >> 2) | ((changeCount - 1) << 6);
      changeCount = 0;

      if (packageStarted) {
        bitCount += 2;
        if (bitCount != 8)
          return;
        bitCount = 0;

        decodeByte(shiftByte);
      } else if (shiftByte == 0xE0) {
        // New package starts here
        bitCount = 0;
        byteCount = 0;
        errorCorBufCount = 0;
        inputBufLen = 0;
        checksum = 0xffff;
        packageStarted = true;
      }
    }
};

#endif  /* RECEIVER_H */
