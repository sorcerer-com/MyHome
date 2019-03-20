import json
import logging
import os
import pkgutil
import time
from configparser import RawConfigParser
from datetime import datetime, timedelta
from shutil import copyfile
from threading import Thread

from Config import Config
from Services import InternetService
from Systems.BaseSystem import BaseSystem
from UIManager import UIManager
from Utils import Utils
from Utils.Decorators import try_catch, type_check
from Utils.Event import Event
from Utils.Singleton import Singleton

logger = logging.getLogger(__name__)

# import all systems
for importer, modname, ispkg in pkgutil.walk_packages(path=["./Systems"], prefix="Systems."):
    __import__(modname)


class MyHome(Singleton):
    """ My Home manager class. """

    _UpdateTime = 1  # seconds
    _UpdateWarningTimeout = 0.1  # seconds

    @type_check
    def __init__(self) -> None:
        """ Initialize an instance of the MyHome class. """

        logger.info("Start My Home")

        self.uiManager = UIManager(self)
        self.config = Config(self)
        self.event = Event()

        # systems
        self.systems = {}
        for cls in BaseSystem.__subclasses__():
            self.systems[cls] = cls(self)

        self._lastBackupTime = datetime.now()
        self.systemChanged = False
        self.load()

        self.setup()

        t = Thread(target=self.update)
        t.daemon = True
        t.start()

        self.event(self, "Start")

    @type_check
    def __repr__(self) -> str:
        """ Return string representation of the object. """

        return str([system.name for system in self.systems.values()])

    @type_check
    def setup(self) -> None:
        """ Setup all sub-components. """

        self.config.setup()
        for system in self.systems.values():
            system.setup()

    @type_check
    def stop(self) -> None:
        """ Save and stop MyHome. """

        self.event(self, "Stop")
        MyHome._UpdateTime = 0

        self.save()
        for system in self.systems.values():
            system.stop()
        logger.info("Stop My Home")

    @type_check
    def update(self) -> None:
        """ Update all the systems rapidly. """

        while MyHome._UpdateTime > 0:
            start = datetime.now()
            # update systems
            for system in self.systems.values():
                if system.isEnabled:
                    system.update()

            if (datetime.now() - start).total_seconds() > MyHome._UpdateWarningTimeout:
                logger.warning("Update time: %s", (datetime.now() - start))

            if self.systemChanged:
                self.save()
                self.systemChanged = False

            time.sleep(MyHome._UpdateTime)

    @type_check
    def load(self) -> None:
        """ Load configurations and systems settings and data. """

        logger.info("Load settings and data")
        configParser = RawConfigParser()
        configParser.optionxform = str
        if Config.ConfigFilePath not in configParser.read(Config.ConfigFilePath):
            return

        data = {}
        if os.path.isfile(Config.DataFilePath):
            with open(Config.DataFilePath, 'r', encoding="utf8") as f:
                data = json.load(f)
            self._lastBackupTime = Utils.parse(
                data["LastBackupTime"], datetime)

        self.config.load(configParser)

        # load systems settings
        keys = sorted(self.systems.keys(), key=lambda x: x.__name__)
        for key in keys:
            if key in self.systems:
                systemData = {}
                if self.systems[key].name in data:
                    systemData = data[self.systems[key].name]
                self.systems[key].load(configParser, systemData)

        self.systemChanged = False
        self.event(self, "Loaded")

    @type_check
    def save(self) -> None:
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
        keys = sorted(self.systems.keys(), key=lambda x: x.__name__)
        for key in keys:
            configParser.add_section(self.systems[key].name)
            systemData = {}
            self.systems[key].save(configParser, systemData)
            data[self.systems[key].name] = systemData
        data = json.dumps(data, indent=4, sort_keys=True, ensure_ascii=True)

        logger.debug("Save config to file: %s", Config.ConfigFilePath)
        with open(Config.ConfigFilePath, 'w') as configFile:
            configParser.write(configFile)

        logger.debug("Save data to file: %s", Config.DataFilePath)
        with open(Config.DataFilePath, 'w') as dataFile:
            dataFile.write(data)

        self.event(self, "SettingsSaved")

    @type_check
    def getSystemByClassName(self, className: str) -> BaseSystem:
        """ Gets system by set class name.

        Arguments:
            className {str} -- Class name of the system.

        Returns:
            BaseSystem -- Instance of the system with the set class name, if there isn't such system None.
        """

        for key in self.systems:
            if key.__name__ == className:
                return self.systems[key]
        return None

    @try_catch("Cannot send alert message")
    @type_check
    def sendAlert(self, msg: str) -> None:
        """ Send alert message through sms and email.

        Arguments:
            msg {str} -- Message text which will be send.
        """

        logger.info("Send alert '%s'", msg)
        # TODO: capture image
        msg = time.strftime("%d/%m/%Y %H:%M:%S") + "\n" + msg + "\n"
        msg += str(self.getSystemByClassName("SensorsSystem").getLatestData())

        smtp_server_info = {"address": self.config.smtpServer, "port": self.config.smtpServerPort,
                            "username": self.config.emailUserName, "password": self.config.emailPassword}
        InternetService.sendEMail(smtp_server_info, self.config.email, [
                                  self.config.email], "My Home", msg, ["test.jpg"])

        # send SMSs only if we are outside of the quiet hours
        quietHours = [int(hour.strip())
                      for hour in self.config.quietHours.split("-")]
        if quietHours[0] > quietHours[1] and (datetime.now().hour > quietHours[0] or datetime.now().hour < quietHours[1]):
            logger.info("Quiet hours: %s", self.config.quietHours)
        elif quietHours[0] < quietHours[1] and datetime.now().hour > quietHours[0] and datetime.now().hour < quietHours[1]:
            logger.info("Quiet hours: %s", self.config.quietHours)
        else:
            InternetService.sendSMS(
                self.config.gsmNumber, "telenor", self.config.myTelenorPassword, msg)
