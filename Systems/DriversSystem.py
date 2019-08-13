import logging
import pkgutil
from configparser import RawConfigParser

from Systems.BaseSystem import BaseSystem
from Systems.Drivers.BaseDriver import BaseDriver
from Utils.Decorators import type_check

logger = logging.getLogger(__name__.split(".")[-1])

# import all systems
for _, modname, _ in pkgutil.walk_packages(["./Systems/Drivers"], "Systems.Drivers."):
    __import__(modname)


class DriversSystem(BaseSystem):
    """ DriversSystem class """

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the DriversSystem class.

        Arguments:
                owner {MyHome} -- MyHome object which is the owner of the system.
        """

        super().__init__(owner)
        self._isEnabled = None

        # TODO: define different types - GenericModule (send command or by request)
        self._drivers = []

    @type_check
    def load(self, configParser: RawConfigParser, data: dict) -> None:
        """ Loads settings and data for the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser from which the settings will be loaded.
                data {dict} -- Dictionary from which the system data will be loaded.
        """

        super().load(configParser, data)

        for name in data:
            self.addDriver(data[name]["type"], name, data[name]["address"])
            self._driversDict[name].load(data[name])

    @type_check
    def save(self, configParser: RawConfigParser, data: dict) -> None:
        """ Saves settings and data used by the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser to which the settings will be saved.
                data {dict} -- Dictionary to which the system data will be saved.
        """

        super().save(configParser, data)

        for name, driver in self._driversDict.items():
            data[name] = {}
            data[name]["type"] = driver.driverType
            data[name]["address"] = driver.address
            driver.save(data[name])

    @property
    @type_check
    def driverTypes(self) -> list:
        """ Gets a list with all driver types. """

        return [cls.__name__.replace("Driver", "") for cls in BaseDriver.__subclasses__()]

    @property
    @type_check
    def _driversDict(self) -> dict:  # name / Driver
        """ Gets a dictionary with drivers names and Driver. """

        return {driver.name: driver for driver in self._drivers}

    @type_check
    def addDriver(self, type_: str, name: str, address: str) -> bool:
        """ Add new driver with the set type, name and address.

        Arguments:
            type_ {str} -- Type of the driver.
            name {str} -- Name of the driver.
            address {str} -- Address of the driver.

        Returns:
            bool -- True if successfully create the driver, otherwise False.
        """

        if name in self._driversDict:
            logger.warning(
                "Try to add driver with name (%s) that already exists", name)
            return False

        for cls in BaseDriver.__subclasses__():
            if cls.__name__ == type_ + "Driver":
                self._drivers.append(cls(name, address))
                self._owner.systemChanged = True
                return True
        return False

    @type_check
    def getDriversByType(self, type_: str) -> list:
        """ Return list with drivers of the set type.

        Arguments:
            type_ {str} -- Type of the drivers.

        Returns:
            list -- List with drivers of the set type.
        """

        result = []
        for driver in self._drivers:
            if driver.driverType == type_:
                result.append(driver)
        return result
