import logging

from Utils.Decorators import type_check

logger = logging.getLogger(__name__.split(".")[-1])


class BaseDriver:
    """ BaseDriver class """

    @type_check
    def __init__(self, name: str, address: str) -> None:
        """ Initialize an instance of the BaseDriver class.

        Arguments:
            name {str} -- Name of the driver.
            address {str} -- (IP) Address of the driver.
        """

        self.name = name
        self.address = address

    @type_check
    def load(self, _: dict) -> None:
        """ Loads driver's data.

        Arguments:
                data {dict} -- Dictionary from which the driver data will be loaded.
        """

        logger.debug("Load driver: %s", self.name)

    @type_check
    def save(self, _: dict) -> None:
        """ Saves driver's data.

        Arguments:
                data {dict} -- Dictionary to which the driver data will be saved.
        """

        logger.debug("Save driver: %s", self.name)

    @property
    @type_check
    def driverType(self) -> str:
        """ Return type of the driver. """

        return self.__class__.__name__.replace("Driver", "")

    @property
    @type_check
    def driverColor(self) -> str:
        """ Return UI color of the driver. """

        return "rgba(128, 128, 128, 0.7)"
