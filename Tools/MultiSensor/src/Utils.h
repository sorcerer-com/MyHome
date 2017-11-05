#ifndef UTILS_H
#define UTILS_H

#define uint  unsigned int
#define ulong unsigned long

#define sec 1000

#define DEBUG

#ifdef DEBUG
# define DEBUG(...) { Serial.print(__VA_ARGS__); }
# define DEBUGLN(...) { Serial.println(__VA_ARGS__); }
#else
# define DEBUG(...) {}
# define DEBUGLN(...) {}
#endif


#if defined(__AVR__)
#include <util/crc16.h>
#endif

static inline uint16_t crc_update(uint16_t crc, uint8_t data)
{
#if defined(__AVR__)
  return _crc_ccitt_update(crc, data);
#else
  // Source: http://www.atmel.com/webdoc/AVRLibcReferenceManual/group__util__crc_1ga1c1d3ad875310cbc58000e24d981ad20.html
  data ^= crc & 0xFF;
  data ^= data << 4;

  return ((((uint16_t)data << 8) | (crc >> 8)) ^ (uint8_t)(data >> 4)
          ^ ((uint16_t)data << 3));
#endif
}

#endif // UTILS_H
