import logging
from datetime import datetime, timedelta

from Systems.BaseSystem import BaseSystem
from Utils.Decorators import try_catch, type_check

logger = logging.getLogger(__name__.split(".")[-1])


class AISystem(BaseSystem):
    """ AISystem class """

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the AISystem class.

        Arguments:
                owner {MyHome} -- MyHome object which is the owner of the system.
        """

        super().__init__(owner)

        self._owner.event += self._onEventReceived

        self.lightOnThreshold = 15
        self.lightOnDuration = 60  # seconds

        self._lightsOn = {}  # light driver / turn on time

    @type_check
    def update(self) -> None:
        """ Update current system's state. """

        toRemove = []
        for driver, time in self._lightsOn.items():
            if datetime.now() > time:
                logger.debug("Light '%s' turn off", driver.name)
                driver.isOn = False
                toRemove.append(driver)
        for item in toRemove:
            del self._lightsOn[item]

    @try_catch("Lighting automation error")
    @type_check
    def _onEventReceived(self, sender: object, event: str, data: object = None) -> None:
        """ Event handler.

        Arguments:
                sender {object} -- Sender of the event.
                event {str} -- Event type.
                data {object} -- Data associated with the event.
        """

        # only motion in data, skip get all data
        if event == "SensorDataAdded" and "Motion" in data and len(data) == 1:
            latestData = sender._readData()  # try to read current data from the sensor
            if latestData is not None:
                latestData = {data["name"]: data["value"]
                              for data in latestData}
            else:
                latestData = sender.latestData
            logger.debug("lighting: %s", latestData["Lighting"])
            if "Lighting" in latestData and latestData["Lighting"] < self.lightOnThreshold:
                lightDrivers = self._owner.systems["DriversSystem"].getDriversByType(
                    "Light")
                for driver in lightDrivers:
                    logger.debug("Light %s: %s (%s)", driver.name, driver.isOn, driver in self._lightsOn)
                    # only light drivers with same name as the sensor
                    if not driver.name.startswith(sender.name):
                        continue
                    # if light is on, but not by the automation
                    if driver.isOn and driver not in self._lightsOn:
                        continue
                    # if end of the motion, but the light isn't turned on by the automation
                    if not data["Motion"] and driver not in self._lightsOn:
                        continue

                    driver.isOn = True
                    duration = self.lightOnDuration * \
                        2 if data["Motion"] else self.lightOnDuration
                    self._lightsOn[driver] = datetime.now() + \
                        timedelta(seconds=duration)
                    logger.debug("Light '%s' turn on until: %s", driver.name, self._lightsOn[driver])
