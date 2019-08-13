import logging
import os
from configparser import RawConfigParser
from datetime import datetime, timedelta

import cv2

from Config import Config
from Systems.BaseSystem import BaseSystem
from Utils.Decorators import type_check

logger = logging.getLogger(__name__.split(".")[-1])


class SecuritySystem(BaseSystem):
    """ SecuritySystem class """

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the SecuritySystem class.

        Arguments:
                owner {MyHome} -- MyHome object which is the owner of the system.
        """

        super().__init__(owner)

        self._owner.event += lambda s, e, d=None: self._onEventReceived(
            s, e, d)

        self.startDelay = timedelta(minutes=15)
        self.sendInterval = timedelta(minutes=5)
        self.numImages = 30

        self._activated = False
        self._startTime = datetime.now() + self.startDelay
        self._prevImages = {}  # camera name / OpenCV image
        self._imageFiles = {}  # camera name / image file name
        self._history = []

    @type_check
    def load(self, configParser: RawConfigParser, data: dict) -> None:
        """ Loads settings and data for the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser from which the settings will be loaded.
                data {dict} -- Dictionary from which the system data will be loaded.
        """

        super().load(configParser, data)

        if "history" in data:
            self._history = data["history"]

        self._startTime = datetime.now() + self.startDelay

    @type_check
    def save(self, configParser: RawConfigParser, data: dict) -> None:
        """ Saves settings and data used by the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser to which the settings will be saved.
                data {dict} -- Dictionary to which the system data will be saved.
        """

        super().save(configParser, data)

        data["history"] = self._history

    @type_check
    def _onEventReceived(self, sender: object, event: str, _: object) -> None:
        """ Event handler.

        Arguments:
            sender {object} -- Sender of the event.
            event {str} -- Event type.
            data {object} -- Data associated with the event.
        """

        if sender != self or event != "IsEnabledChanged":
            return

        if self._activated:
            self._addHistory("Alarm deactivated")
        self._activated = False
        self._startTime = datetime.now() + self.startDelay
        self._prevImages.clear()
        self._clearImages()

    @type_check
    def update(self) -> None:
        """ Update current system's state. """

        super().update()

        elapsed = datetime.now() - self._startTime
        # send alert
        if elapsed > self.sendInterval:
            self._activated = False
            if len(self._imageFiles) > 0 or self._checkForOfflineCamera():
                self._addHistory("Send alert")
                files = [Config.BinPath + item for _,
                         value in self._imageFiles.items() for item in value]
                if self._owner.sendAlert("Security Alarm Activated!", files, True):
                    self._clearImages()
            else:
                self._addHistory("Skip alert sending")

        # check for motion - if not activated and after delay start
        if not self._activated and elapsed > timedelta():
            self._activated = self._owner.systems["SensorsSystem"].isMotionDetected
            if self._activated:
                logger.info("Alarm Activated")
                self._addHistory("Alarm activated")
                self._owner.event(self, "AlarmActivated")
            self._startTime = datetime.now()

        # get images from cameras and check for movement
        if self._activated:
            for camera in self._owner.systems["SensorsSystem"]._cameras:
                if camera.capture.isOpened():
                    img = camera.getImage()
                    if camera.name not in self._prevImages:  # first image taken
                        self._prevImages[camera.name] = img
                    elif self._findMovement(self._prevImages[camera.name], img):
                        self._saveImage(camera.name, img)
                        self._prevImages[camera.name] = img

    @type_check
    def _saveImage(self, cameraName: str, img: object) -> None:
        """ Save the set image on the disk.

        Arguments:
            cameraName {str} -- Camera name.
            img {object} -- OpenCV image to be saved.
        """

        if cameraName not in self._imageFiles:  # first image
            self._imageFiles[cameraName] = [cameraName + "0.jpg"]
            cv2.imwrite(
                Config.BinPath + self._imageFiles[cameraName][-1], self._prevImages[cameraName])

        self._imageFiles[cameraName].append(
            cameraName + str(len(self._imageFiles[cameraName])) + ".jpg")
        cv2.imwrite(Config.BinPath +
                    self._imageFiles[cameraName][-1], img)

    @type_check
    def _clearImages(self) -> None:
        """ Clear saved cameras images. """

        for _, value in self._imageFiles.items():
            for file in value:
                if os.path.isfile(Config.BinPath + file):
                    os.remove(Config.BinPath + file)
        self._imageFiles.clear()

    @type_check
    def _checkForOfflineCamera(self) -> bool:
        """ Return true if there is a offline camera. """

        for camera in self._owner.systems["SensorsSystem"]._cameras:
            if not camera.capture.isOpened():
                return True
        return False

    @type_check
    def _addHistory(self, entry: str) -> None:
        """ Add entry to history. 

        Arguments:
            entry {str} -- Entry message which will be added.
        """

        self._history.append(f"{datetime.now():%Y-%m-%d %H:%M:%S} {entry}")
        self._history = self._history[-500:]  # keep last 500 entries
        self._owner.systemChanged = True

    @type_check
    def _findMovement(self, image1: object, image2: object) -> bool:
        """ Find movement between two OpenCV images.

        Arguments:
            image1 {object} -- First OpenCV image.
            image2 {object} -- Second OpenCV image.

        Returns:
            bool -- True if there is a movement between the two OpenCV images.
        """

        if image1 is None or image2 is None or \
                image1.shape != image2.shape:
            return False

        diff = cv2.subtract(image2, image1)
        diff = cv2.cvtColor(diff, cv2.COLOR_BGR2GRAY)  # to gray
        _, diff = cv2.threshold(diff, 32, 255, cv2.THRESH_BINARY)  # binarize
        diffValue = diff.mean() 
        if diffValue > 0.01 * 255:
            logger.debug("Camera movement value: %s", diffValue)
            self._addHistory(f"Camera movement value: {diffValue}")
            return True
        return False
