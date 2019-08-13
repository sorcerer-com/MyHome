import logging
from datetime import datetime, timedelta

from flux_led import WifiLedBulb

from Systems.Drivers.BaseDriver import BaseDriver
from Utils.Decorators import type_check

logger = logging.getLogger(__name__.split(".")[-1])


class LightDriver(BaseDriver):
    """ LightDriver class """

    @type_check
    def __init__(self, name: str, address: str) -> None:
        """ Initialize an instance of the LightDriver class.

        Arguments:
            name {str} -- Name of the driver.
            address {str} -- (IP) Address of the driver.
        """

        super().__init__(name, address)

        self._light = None
        self._lastUse = datetime.now() - timedelta(minutes=1)

        self._isOn = None
        self._color = None
        self._warmWhite = None

    @property
    @type_check
    def driverColor(self) -> str:
        """ Return UI color of the driver. """

        return "rgba(255, 217, 0, 0.6)"

    @property
    @type_check
    def light(self) -> WifiLedBulb:
        """ Gets an instance of WifiLedBulb. """

        # try to reconnect every 1 minute (even if it's connected successfully)
        if datetime.now() - self._lastUse > timedelta(minutes=1):
            self._lastUse = datetime.now()
            try:
                self._light = WifiLedBulb(self.address, timeout=1)
            except Exception:
                logger.exception("Cannot connect to the light")
        return self._light

    @property
    @type_check
    def isOn(self) -> bool:
        """ Gets whether the light is on. """

        if self.light is None:
            return False
        if self._isOn is None:
            self._isOn = self.light.isOn()
        return self._isOn

    @isOn.setter
    @type_check
    def isOn(self, value: bool) -> None:
        """ Sets whether the light is on. """

        if self.light is not None and self.isOn != value:
            if value:
                self.light.turnOn()
                self._isOn = True
            else:
                self.light.turnOff()
                self._isOn = False

    @property
    @type_check
    def red(self) -> int:
        """ Gets the red component of the color. """

        if self.light is None:
            return 0
        if self._color is None:
            self._color = list(self.light.getRgb())
        return self._color[0]

    @red.setter
    @type_check
    def red(self, value: int) -> None:
        """ Sets the red component of the color. """

        if self.light is not None and self.red != value:
            self.light.setRgb(value, self._color[1], self._color[2])
            self._color[0] = value

    @property
    @type_check
    def green(self) -> int:
        """ Gets the green component of the color. """

        if self.light is None:
            return 0
        if self._color is None:
            self._color = list(self.light.getRgb())
        return self._color[1]

    @green.setter
    @type_check
    def green(self, value: int) -> None:
        """ Sets the green component of the color. """

        if self.light is not None and self.green != value:
            self.light.setRgb(self._color[0], value, self._color[2])
            self._color[1] = value

    @property
    @type_check
    def blue(self) -> int:
        """ Gets the blue component of the color. """

        if self.light is None:
            return 0
        if self._color is None:
            self._color = list(self.light.getRgb())
        return self._color[2]

    @blue.setter
    @type_check
    def blue(self, value: int) -> None:
        """ Sets the blue component of the color. """

        if self.light is not None and self.blue != value:
            self.light.setRgb(self._color[0], self._color[1], value)
            self._color[2] = value

    @property
    @type_check
    def warmWhite(self) -> int:
        """ Gets the warm white level. """

        if self.light is None:
            return 0
        if self._warmWhite is None:
            self._warmWhite = self.light.getWarmWhite255()
        return self._warmWhite

    @warmWhite.setter
    @type_check
    def warmWhite(self, value: int) -> None:
        """ Sets the warm white level. """

        if self.light is not None and self.warmWhite != value:
            self.light.setWarmWhite255(value)
            self._warmWhite = value
