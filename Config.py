import logging
from configparser import RawConfigParser

from Utils import Utils
from Utils.Decorators import type_check

logger = logging.getLogger(__name__)


class Config:
    """ Configuration class. """

    LogFilePath = "bin/log.log"
    ConfigFilePath = "bin/config.ini"
    DataFilePath = "bin/data.json"

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the Config class.

        Arguments:
            owner {MyHome} -- MyHome object which is the owner of the config.
        """

        self._owner = owner

        self.appSecret = ""
        self.password = ""
        self.token = ""
        self.internalIPs = []

    @type_check
    def __repr__(self) -> str:
        """ Return string representation of the object. """

        return str({name: getattr(self, name) for name in Utils.getFields(self)})

    @type_check
    def setup(self) -> None:
        """ Setup config. """

        uiContainer = self._owner.uiManager.registerContainer(self)
        uiContainer.properties["appSecret"].isPrivate = True

    @type_check
    def load(self, configParser: RawConfigParser) -> bool:
        """ Loads configurations from the config parser.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser which will be used to load configurations.

        Returns:
                bool -- True if the loading is successful, otherwise False.
        """

        logger.debug("Load Config")
        items = configParser.items(self.__class__.__name__)
        for (name, value) in items:
            if hasattr(self, name):
                valueType = type(getattr(self, name))
                value = Utils.parse(value, valueType)
                setattr(self, name, value)
                logger.debug("Config - %s: %s (%s)", name, value,valueType)
        return True

    @type_check
    def save(self, configParser: RawConfigParser) -> None:
        """ Saves configurations to the config parser.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser which will be used to save configurations.
        """

        logger.debug("Save Config")
        section = self.__class__.__name__
        configParser.add_section(section)
        items = Utils.getFields(self)
        for name in items:
            value = getattr(self, name)
            configParser.set(section, name, Utils.string(value))
            logger.debug("Config - %s: %s", name, value)
