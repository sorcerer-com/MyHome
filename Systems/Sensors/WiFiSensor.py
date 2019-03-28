import logging
import random

from Services import InternetService
from Systems.Sensors.BaseSensor import BaseSensor
from Utils.Decorators import type_check

logger = logging.getLogger(__name__.split(".")[-1])


class WiFiSensor(BaseSensor):
    """ WiFiSensor class """

    @type_check
    def __init__(self, name: str, address: str) -> None:
        """ Initialize an instance of the Sensors class.

        Arguments:
            name {str} -- Name of the sensor.
            address {str} -- (IP) Address of the sensor.
        """

        super().__init__(name, address)

    @type_check
    def _readData(self) -> list:
        """ Read data from the WiFi sensor. """

        return self._temp()
        return InternetService.getJsonContent(f"http://{self.address}/data")

    # TODO: remove:
    def _temp(self):
        return [
            {"name": "Power1", "value": random.randrange(10000, 50000) / 100,
                "aggrType": "avg", "desc": "description"},
            {"name": "ConsumedPower1", "value": self._lastReadings["ConsumedPower1"] + random.randrange(10000, 15000) / 100,
                "aggrType": "sum", "desc": "description"}
        ]
