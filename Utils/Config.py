import logging
from configparser import RawConfigParser
from os import path

from Utils import Utils
from Utils.Decorators import type_check

logger = logging.getLogger(__name__)


class Config(object):
    """ Configuration class. """

    LogFilePath = "bin/log.log"
    ConfigFilePath = "bin/config.ini"
    DataFilePath = "bin/data.json"

    @type_check
    def __init__(self):
        """ Initialize Config instace. """

        self.appSecret = ""

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
                logger.debug("Config - %s: %s (%s)" %
                             (name, value, valueType))
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
            logger.debug("Config - %s: %s" % (name, value))
