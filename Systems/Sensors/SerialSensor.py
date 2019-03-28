import json
import logging
import random

from serial import Serial

from Systems.Sensors.BaseSensor import BaseSensor
from Utils.Decorators import type_check

logger = logging.getLogger(__name__.split(".")[-1])


class SerialSensor(BaseSensor):
    """ SerialSensor class """

    @type_check
    def __init__(self, name: str, address: str) -> None:
        """ Initialize an instance of the Sensors class.

        Arguments:
            name {str} -- Name of the sensor.
            address {str} -- (IP) Address of the sensor.
        """

        super().__init__(name, address)

        self._serial = None

    @type_check
    def __del__(self) -> None:
        """ Close Serial on delete. """

        if self._serial:
            self._serial.close()

    @type_check
    def update(self) -> None:
        """ Update current sensor's state. """

        # TODO: test
        if self._serial is None:
            return

        try:
            while self._serial.in_waiting > 0:
                line = self._serial.readline().strip()
                if line.startswith("//"):
                    continue

                data = json.loads(line.replace("'", "\""))
                self.addData(self.latestTime, data)
        except Exception:
            logger.exception(
                "Cannot read data from serial sensor: %s(%s)", self.name, self.address)
            self._serial.close()
            self._serial = None

    @type_check
    def _readData(self) -> list:
        """ Read data from the Serial sensor. """

        return self._temp()
        # TODO: test
        try:
            if not self._serial:
                self._serial = Serial(
                    self.address, baudrate=9600, timeout=2, write_timeout=2)  # open serial port
            # TODO: wait?
            self._serial.reset_input_buffer()  # clear input buffer
            self._serial.write("getdata")
            # TODO: flush? wait?
            return json.loads(self._serial.readline().replace("'", "\""))
        except Exception:
            logger.exception(
                "Cannot read data from serial sensor: %s(%s)", self.name, self.address)
            self._serial.close()
            self._serial = None
            return None

    # TODO: remove:
    def _temp(self):
        return [
            {"name": "Motion", "value": (random.randrange(1, 10) > 5),
                "aggrType": "avg", "desc": "description"},
            {"name": "Temperature", "value": random.randrange(500, 1500) / 10,
                "aggrType": "avg", "desc": "description"},
            {"name": "Humidity", "value": random.randrange(500, 1000) / 10,
                "aggrType": "avg", "desc": "description"},
            {"name": "Smoke", "value": random.randrange(1, 100),
                "aggrType": "avg", "desc": "description"},
            {"name": "Lighting", "value": random.randrange(1, 100),
                "aggrType": "avg", "desc": "description"}
        ]
