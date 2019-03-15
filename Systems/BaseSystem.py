import logging
from configparser import RawConfigParser

from Utils import Utils
from Utils.Decorators import type_check

logger = logging.getLogger(__name__)


class BaseSystem:
    """ BaseSystem class """

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the BaseSystem class.

        Arguments:
                owner {MyHome} -- MyHome object which is the owner of the system.
        """

        self._owner = owner
        self._isEnabled = True
        self.isVisible = True

    @type_check
    def __repr__(self) -> str:
        """ Return string representation of the object. """

        return str({name: getattr(self, name) for name in Utils.getFields(self)})

    @type_check
    def setup(self) -> None:
        """ Setup the system. """

        uiContainer = self._owner.uiManager.registerContainer(self)
        uiContainer.properties["isVisible"].isPrivate = True

    @type_check
    def stop(self) -> None:
        """ Stop current system. """
        logger.debug("Stop system: %s", self.name)

    @type_check
    def load(self, configParser: RawConfigParser, data: dict) -> None:
        """ Loads settings and data for the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser from which the settings will be loaded.
                data {dict} -- Dictionary from which the system data will be loaded.
        """

        logger.debug("Load system: %s", self.name)
        if not configParser.has_section(self.name):
            return

        items = configParser.items(self.name)
        for (name, value) in items:
            # static fields
            if hasattr(type(self), name):
                valueType = type(getattr(type(self), name))
                if valueType is not property:
                    setattr(type(self), name, Utils.parse(value, valueType))
            # instance fields
            if hasattr(self, name):
                valueType = type(getattr(self, name))
                setattr(self, name, Utils.parse(value, valueType))
            logger.debug("%s System - %s: %s (%s)",
                         self.name, name, value, valueType)

    @type_check
    def save(self, configParser: RawConfigParser, data: dict) -> None:
        """ Saves settings and data used from the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser to which the settings will be saved.
                data {dict} -- Dictionary to which the system data will be saved.
        """

        logger.debug("Save system: %s", self.name)
        items = Utils.getFields(self)
        for name in items:
            value = getattr(self, name)
            configParser.set(self.name, name, Utils.string(value))
            logger.debug("%s System - %s: %s", self.name, name, value)

    @type_check
    def update(self) -> None:
        """ Update current system's state. """
        logger.debug("Update system: %s", self.name)

    @property
    @type_check
    def name(self) -> str:
        """ Gets name of the system.

        Returns:
            str -- Name of the system.
        """

        return self.__class__.__name__[:-len("System")]

    @property
    @type_check
    def isEnabled(self) -> bool:
        """ Gets a value indicating whether the system is enabled.

        Returns:
                bool -- True if the system is enabled, otherwise false.
        """
        return self._isEnabled

    @isEnabled.setter
    @type_check
    def isEnabled(self, value: bool) -> None:
        """ Sets a value indicating whether the system is enabled.

        Arguments:
                value {bool} -- If true the system will be enabled, otherwise it'll be disabled.
        """

        if self.isEnabled != value:
            self._isEnabled = value
            self._owner.systemChanged = True

            logger.info("%s System %s", self.name,
                        ("enabled" if self.isEnabled else "disabled"))
            self._owner.event(self, "IsEnabledChanged", self.isEnabled)
