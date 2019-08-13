
import logging
from configparser import RawConfigParser
from datetime import datetime, timedelta

from Systems.BaseSystem import BaseSystem
from Utils.Decorators import type_check

logger = logging.getLogger(__name__.split(".")[-1])


class ScheduleSystem(BaseSystem):
    """ ScheduleSystem class """

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the ScheduleSystem class.

        Arguments:
                owner {MyHome} -- MyHome object which is the owner of the system.
        """

        super().__init__(owner)

        self._schedule = []
        self._nextTime = datetime.now()

    @type_check
    def load(self, configParser: RawConfigParser, data: dict) -> None:
        """ Loads settings and data for the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser from which the settings will be loaded.
                data {dict} -- Dictionary from which the system data will be loaded.
        """

        super().load(configParser, data)

        if "schedule" in data:
            self._schedule = data["schedule"]

    @type_check
    def save(self, configParser: RawConfigParser, data: dict) -> None:
        """ Saves settings and data used by the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser to which the settings will be saved.
                data {dict} -- Dictionary to which the system data will be saved.
        """

        super().save(configParser, data)

        data["schedule"] = self._schedule

    @type_check
    def update(self) -> None:
        """ Update current system's state. """

        super().update()

        if datetime.now() < self._nextTime:
            return

        self._schedule.sort(key=lambda x: x["Time"])
        toRemove = []
        for item in self._schedule:
            if datetime.now() > item["Time"]:
                command = item["Command"]
                if "." in command:
                    command = command.replace("MyHome.", "self._owner.")
                    for name, system in self._owner.systems.items():
                        command = command.replace(
                            f"{system.name}.", f"self._owner.systems['{name}'].")

                try:
                    # pylint: disable=exec-used
                    exec(command)
                except Exception:
                    logger.exception("Cannot execute '%s'", command)
                self._owner.event(self, "CommandExecuted", command)

                if item["Repeat"].total_seconds() == 0:
                    toRemove.append(item)
                else:
                    while item["Time"] < datetime.now():  # skip old ones
                        item["Time"] += item["Repeat"]
                self._owner.systemChanged = True
            else:
                break
        for item in toRemove:
            self._schedule.remove(item)
        self._schedule.sort(key=lambda x: x["Time"])

        if len(self._schedule) > 0:
            self._nextTime = self._schedule[0]["Time"]
        else:
            self._nextTime += timedelta(seconds=1)
