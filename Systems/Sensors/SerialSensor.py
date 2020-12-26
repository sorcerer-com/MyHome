import json
import logging
import time

from serial import Serial

from Systems.Sensors.BaseSensor import BaseSensor
from Utils.Decorators import type_check

logger = logging.getLogger(__name__.split(".")[-1])


class SerialSensor(BaseSensor):
    """ SerialSensor class """

    @type_check
    def __init__(self, owner: None, name: str, address: str) -> None:
        """ Initialize an instance of the Sensors class.

        Arguments:
            owner {SensorSystem} -- SensorSystem object which is the owner of the sensor.
            name {str} -- Name of the sensor.
            address {str} -- (IP) Address of the sensor.
        """

        super().__init__(owner, name, address)

        self._serial = None

    @type_check
    def __del__(self) -> None:
        """ Close Serial on delete. """

        if self._serial:
            self._serial.close()

    @type_check
    def update(self) -> None:
        """ Update current sensor's state. """

        super().update()

        if self._serial is None:
            return

        try:
            while self._serial.in_waiting > 0:
                line = self._serial.readline().decode("utf-8").strip()
                if line.startswith("//"):
                    continue

                data = json.loads(line.replace("'", "\""))
                self.addData(self.latestTime, data)
        except Exception:
            logger.exception(
                "Cannot read data from serial sensor: %s(%s)", self.name, self.address)
            if self._serial:
                self._serial.close()
            self._serial = None

    @type_check
    def _readData(self) -> list:
        """ Read data from the Serial sensor. """

        try:
            if not self._serial:
                self._serial = Serial(
                    self.address, baudrate=9600, timeout=2, write_timeout=2)  # open serial port
                time.sleep(2)
            self._serial.reset_input_buffer()  # clear input buffer
            self._serial.write("getdata".encode("utf-8"))
            self._serial.flush()
            self._serial.readline()  # comment "// Received: ..."
            return json.loads(self._serial.readline().decode("utf-8").replace("'", "\""))
        except Exception:
            logger.exception(
                "Cannot read data from serial sensor: %s(%s)", self.name, self.address)
            if self._serial:
                self._serial.close()
            self._serial = None
            return None
