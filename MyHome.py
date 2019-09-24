import json
import logging
import os
import pkgutil
import time
from configparser import RawConfigParser
from datetime import datetime, timedelta
from shutil import copyfile
from threading import Thread

from git import Repo

from Config import Config
from Services import InternetService
from Systems.BaseSystem import BaseSystem
from UIManager import UIManager
from Utils import Utils
from Utils.Decorators import try_catch, type_check
from Utils.Event import Event
from Utils.Singleton import Singleton

logger = logging.getLogger(__name__.split(".")[-1])

# import all systems
for _, modname, _ in pkgutil.walk_packages(["./Systems"], "Systems."):
    __import__(modname)


class MyHome(Singleton):
    """ My Home manager class. """
    # TODO: check github todo list

    _UpdateTime = 1  # seconds
    _UpdateWarningTimeout = 3  # seconds

    @type_check
    def __init__(self) -> None:
        """ Initialize an instance of the MyHome class. """

        logger.info("Start My Home")
        try:
            with Repo(".") as repo:
                logger.info("Version: %s %s", Utils.string(
                    repo.head.commit.committed_datetime), repo.head.commit.message.strip())
        except Exception:
            pass

        self.uiManager = UIManager(self)
        self.config = Config(self)
        self.event = Event()

        # systems
        self.systems = {}
        for cls in BaseSystem.__subclasses__():
            self.systems[cls.__name__] = cls(self)

        self._lastBackupTime = datetime.now()
        self.systemChanged = False
        self.load()

        self.setup()

        self.upgradeAvailable = False
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

        logger.info("Setup My Home")
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

            if start.minute % 5 == 0 and start.second == 0:
                self.upgradeAvailable = self.upgrade(True)

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
            if "LastBackupTime" in data:
                self._lastBackupTime = Utils.parse(
                    data["LastBackupTime"], datetime)

        self.config.load(configParser)

        # load systems settings
        keys = sorted(self.systems.keys())
        for key in keys:
            if key in self.systems:
                systemData = {}
                if self.systems[key].name in data:
                    systemData = Utils.deserializable(
                        data[self.systems[key].name])
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
        keys = sorted(self.systems.keys())
        for key in keys:
            configParser.add_section(self.systems[key].name)
            systemData = {}
            self.systems[key].save(configParser, systemData)
            data[self.systems[key].name] = Utils.serializable(systemData)
        data = json.dumps(data, indent=4, ensure_ascii=True)

        logger.debug("Save config to file: %s", Config.ConfigFilePath)
        with open(Config.ConfigFilePath, 'w') as configFile:
            configParser.write(configFile)

        logger.debug("Save data to file: %s", Config.DataFilePath)
        with open(Config.DataFilePath, 'w') as dataFile:
            dataFile.write(data)

        self.event(self, "SettingsSaved")

    @try_catch("Cannot send alert message", False)
    @type_check
    def sendAlert(self, msg: str, files: list = None, force: bool = False) -> bool:
        """ Send alert message through sms and email.

        Arguments:
            msg {str} -- Message text which will be send.

        Keyword Arguments:
            files {list} -- List of files to be sent to email. Default - attach images from cameras.  (default: {None})
            force {bool} -- If true force send sms ignoring the quiet hours. (default: {False})

        Returns:
            bool -- True if successfully send alert, otherwise false.
        """

        logger.info("Send alert '%s'", msg)

        result = True
        msg = time.strftime("%d/%m/%Y %H:%M:%S") + "\n" + msg + "\n"
        msg += str(self.systems["SensorsSystem"].latestData)

        files2 = []
        if files is None:
            for camera in self.systems["SensorsSystem"]._cameras:
                camera.saveImage(Config.BinPath + camera.name + ".jpg")
                files2.append(Config.BinPath + camera.name + ".jpg")

        smtp_server_info = {"address": self.config.smtpServer, "port": self.config.smtpServerPort,
                            "username": self.config.emailUserName, "password": self.config.emailPassword}
        if not InternetService.sendEMail(smtp_server_info, self.config.email, [self.config.email], "My Home", msg, files or files2):
            result = False

        for file in files2:
            if os.path.isfile(file):
                os.remove(file)

        # send SMSs only if we are outside of the quiet hours
        quietHours = [int(hour.strip())
                      for hour in self.config.quietHours.split("-")]
        if force:
            quietHours = [0, 0]
        if quietHours[0] > quietHours[1] and (datetime.now().hour > quietHours[0] or datetime.now().hour < quietHours[1]):
            logger.info("Quiet hours: %s", self.config.quietHours)
        elif quietHours[0] < quietHours[1] and datetime.now().hour > quietHours[0] and datetime.now().hour < quietHours[1]:
            logger.info("Quiet hours: %s", self.config.quietHours)
        else:
            if not InternetService.sendSMS(self.config.gsmNumber, "telenor", self.config.myTelenorPassword, msg):
                result = False

        if result is False:
            InternetService.sendEMail(smtp_server_info, self.config.email, [
                                      self.config.email], "My Home", "Alert sending failed")
        return result

    @try_catch("Cannot (check for) system upgrade", False)
    @type_check
    def upgrade(self, checkOnly: bool) -> bool:
        """ Check or upgrade the system from the remote repository.

        Arguments:
            checkOnly {bool} -- True if only want to check for available upgrade.

        Returns:
            bool -- If check only for upgrade - True if there is, otherwise False. If upgrade True if it's successfull.
        """

        if checkOnly:
            logger.debug("Check for system update")

        with Repo(".") as repo:
            for info in repo.remote().fetch():
                if info.name == "origin/" + repo.active_branch.name:
                    if checkOnly:
                        return repo.head.commit.hexsha != info.commit.hexsha
                    else:
                        logger.info("Upgrading...")
                        repo.remote().pull()
                    break
            return True
