import json
import logging
import os
import time
from configparser import RawConfigParser
from datetime import datetime, timedelta
from shutil import copyfile
from threading import Thread

from Systems.BaseSystem import BaseSystem
from Utils import Utils
from Utils.Config import Config
from Utils.Decorators import type_check
from Utils.Event import Event
from Utils.Singleton import Singleton

logger = logging.getLogger(__name__)


class MyHome(Singleton):
    """ My Home manager class. """

    _UpdateTime = 1  # seconds
    _UpdateWarningTimeout = 0.1  # seconds

    @type_check
    def __init__(self):
        """ Initialize an instance of the MyHome class. """

        logger.info("Start My Home")

        self.config = Config()
        self.event = Event()

        # systems
        self.systems = {}
        for cls in BaseSystem.__subclasses__():
            self.systems[cls.Name] = cls(self)

        self._lastBackupTime = datetime.now()
        self.systemChanged = False
        self.load()

        t = Thread(target=self.update)
        t.daemon = True
        t.start()

        self.event(self, "Start")

    def __del__(self):
        """ Destroy the MyHome instance. """
        self.stop()

    @type_check
    def stop(self) -> None:
        """ Save and stop MyHome. """

        self.event(self, "Stop")
        MyHome._UpdateTime = 0

        self.save()
        for key in self.systems.keys():
            self.systems[key].stop()
        logger.info("Stop My Home")

    @type_check
    def update(self) -> None:
        """ Update all the systems rapidly. """

        while(MyHome._UpdateTime > 0):
            start = datetime.now()
            # update systems
            for system in self.systems.values():
                if system.isEnabled:
                    system.update()

            if self.systemChanged:
                self.save()
                self.systemChanged = False

            if (datetime.now() - start).total_seconds() > MyHome._UpdateWarningTimeout:
                logger.warn("Update time: %s" % str(datetime.now() - start))

            time.sleep(MyHome._UpdateTime)

    @type_check
    def load(self) -> None:
        """ Load configurations and systems settings and data. """

        logger.info("Load settings and data")
        configParser = RawConfigParser()
        configParser.optionxform = str
        if Config.ConfigFilePath not in configParser.read(Config.ConfigFilePath):
            return

        data = []
        if os.path.isfile(Config.DataFilePath):
            with open(Config.DataFilePath, 'r', encoding="utf8") as f:
                data = json.load(f)
            self._lastBackupTime = Utils.parse(
                data["LastBackupTime"], datetime)

        self.config.load(configParser)

        # load systems settings
        keys = sorted(self.systems.keys())
        for key in keys:
            if key in data:
                self.systems[key].load(configParser, data[key])

        self.systemChanged = False
        self.event(self, "Loaded")

    @type_check
    def save(self):
        """ Save configurations and systems settings and data. """

        logger.info("Save settings and data")
        # backup config and data file every day
        if (datetime.now() - self._lastBackupTime) > timedelta(days=1):
            self._lastBackupTime = datetime.now()
            copyfile(Config.ConfigFilePath, Config.ConfigFilePath + ".bak")
            copyfile(Config.DataFilePath, Config.DataFilePath + ".bak")

        configParser = RawConfigParser()
        configParser.optionxform = str

        self.config.save(configParser)

        # save systems settings
        data = {}
        data["LastBackupTime"] = Utils.string(self._lastBackupTime)
        keys = sorted(self.systems.keys())
        for key in keys:
            configParser.add_section(self.systems[key].Name)
            systemData = {}
            self.systems[key].save(configParser, systemData)
            data[self.systems[key].Name] = systemData
        data = json.dumps(data, indent=4, sort_keys=True, ensure_ascii=True)

        logger.debug("Save config to file: %s", Config.ConfigFilePath)
        with open(Config.ConfigFilePath, 'w') as configFile:
            configParser.write(configFile)

        logger.debug("Save data to file: %s", Config.DataFilePath)
        with open(Config.DataFilePath, 'w') as dataFile:
            dataFile.write(data)

        self.event(self, "SettingsSaved")
