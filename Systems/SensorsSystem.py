import logging
from configparser import RawConfigParser
from datetime import datetime, timedelta

from Utils.Decorators import try_catch, type_check
from Utils.TaskManager import TaskManager

from Systems.BaseSystem import BaseSystem
from Systems.Sensors.Camera import Camera
from Systems.Sensors.SerialSensor import SerialSensor
from Systems.Sensors.WiFiSensor import WiFiSensor

logger = logging.getLogger(__name__.split(".")[-1])


class SensorsSystem(BaseSystem):
    """ SensorsSystem class """

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the SensorsSystem class.

        Arguments:
                owner {MyHome} -- MyHome object which is the owner of the system.
        """

        super().__init__(owner)

        self.checkInterval = 15
        self.alerts = {}  # valueName / threshold

        self._sensors = []
        self._cameras = []

    @type_check
    def setup(self) -> None:
        """ Setup the system. """

        super().setup()

        self._owner.uiManager.containers[self].properties[
            "alerts"].hint = "Value Name / Threshold \nThe value can start with '~' to match part of the name. \nThe threshold can start with special symbol '>', '<' or '='"

        self._owner.uiManager.containers[self].properties[
            "sensors"].hint = "Name / Address(Token) (leave empty for event based sensor) \nWARNING: If remove sensor, associated data will be lost"

        self._owner.uiManager.containers[self].properties[
            "cameras"].hint = "Name / Address(Device Index) \nThe address for ONVIF cameras should be - username:password@ip:port, or direct rtsp url"

        self._nextTime = datetime.now().replace(minute=0, second=0, microsecond=0)
        # the exact self.checkInterval minute in the hour
        while self._nextTime < datetime.now():
            self._nextTime += timedelta(minutes=self.checkInterval)

    @type_check
    def load(self, configParser: RawConfigParser, data: dict) -> None:
        """ Loads settings and data for the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser from which the settings will be loaded.
                data {dict} -- Dictionary from which the system data will be loaded.
        """

        super().load(configParser, data)

        for name in data:
            if name.endswith("Sensor") and name[:-len("Sensor")] in self._sensorsDict:
                self._sensorsDict[name[:-len("Sensor")]].load(data[name])
            elif name.endswith("Camera") and name[:-len("Camera")] in self._camerasDict:
                self._camerasDict[name[:-len("Camera")]].load(data[name])

    @type_check
    def save(self, configParser: RawConfigParser, data: dict) -> None:
        """ Saves settings and data used by the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser to which the settings will be saved.
                data {dict} -- Dictionary to which the system data will be saved.
        """

        super().save(configParser, data)

        for name, sensor in self._sensorsDict.items():
            data[name + "Sensor"] = {}
            sensor.save(data[name + "Sensor"])

        for name, camera in self._camerasDict.items():
            data[name + "Camera"] = {}
            camera.save(data[name + "Camera"])

    @type_check
    def update(self) -> None:
        """ Update current system's state. """

        super().update()

        for sensor in self._sensors:
            TaskManager().execute(sensor, sensor.update)
        for camera in self._cameras:
            TaskManager().execute(camera, camera.update)

        if datetime.now() < self._nextTime.replace(second=59):
            return

        # if checkInterval is changed
        if self._nextTime.minute % self.checkInterval != 0:
            self._nextTime = datetime.now().replace(minute=0, second=0, microsecond=0)
            while self._nextTime < datetime.now():
                self._nextTime += timedelta(minutes=self.checkInterval)

        alertMsg = ""
        for sensor in self._sensors + self._cameras:
            if sensor.address == "":
                continue

            logger.debug("Requesting data from %s(%s) sensor",
                         sensor.name, sensor.address)
            data = sensor.readData(self._nextTime)
            if data is not None:
                self._owner.systemChanged = True
                alert = self._check_data(data)
                if alert and alert != "":
                    alertMsg += f"{sensor.name}({alert}) "
            elif not isinstance(sensor, Camera) or not sensor.isIPCamera:
                # alert if the sensor isn't active for more then 4 check intervals
                logger.warning("No data from %s sensor", sensor.name)
                if (sensor.latestTime is not None and
                    sensor.latestTime <= self._nextTime - timedelta(minutes=self.checkInterval*4) and
                        sensor.latestTime > self._nextTime - timedelta(minutes=self.checkInterval*5)):
                    alertMsg += f"{sensor.name}(inactive) "

        if alertMsg != "":
            self._owner.sendAlert(f"{alertMsg.strip()} Alarm Activated!")

        self._nextTime += timedelta(minutes=self.checkInterval)

    @type_check
    def processData(self, token: str, data: list) -> bool:
        """ Process external data from sensor with the set token.

        Arguments:
            token {str} -- Token of the sensor.
            data {list} -- Data which will be processed.

        Returns:
            bool -- True if successfully process the data, otherwise False.
        """

        sensors = [
            sensor for sensor in self._sensors if sensor.token == token]
        if len(sensors) == 0:
            logger.warning(
                "Try to process data(%s) with invalid token: %s", data, token)
            return False

        time = datetime.now().replace(microsecond=0)
        addBiggerOnly = False
        if sensors[0].address != "":  # if it's a pull data sensor modify last data, don't log and add only bigger value
            time = sensors[0].latestTime
            addBiggerOnly = True
        else:
            logger.info("Process external data(%s) for sensor(%s)",
                        data, sensors[0].name)

        if sensors[0].addData(time, data, addBiggerOnly):
            self._owner.systemChanged = True
            alert = self._check_data(data)
            if alert and alert != "":
                self._owner.sendAlert(
                    f"{sensors[0].name}({alert.strip()}) Alarm Activated!")
        return True

    @property
    @type_check
    def _sensorsDict(self) -> dict:  # name / Sensor
        """ Gets a dictionary with sensors names and Sensor. """

        return {sensor.name: sensor for sensor in self._sensors}

    @property
    @type_check
    def sensors(self) -> dict:  # name / address (token)
        """ Gets a dictionary with sensors names and addresses / tokens. """

        # if sensor.address == "" then show token instead to be able to rename such sensors
        # this leads to save token value in config.ini, so fix sensor.address value in BaseSensor.load
        result = {}
        for sensor in self._sensors:
            if sensor.address != "":
                result[sensor.name] = sensor.address
            else:
                result[sensor.name] = sensor.token
        return result

    @sensors.setter
    @type_check
    def sensors(self, value: dict) -> None:
        """ Sets the dictionary with sensors names and addresses / tokens. """

        for name, address in value.items():
            # address is actually token if sensor.address == ""
            if name not in self._sensorsDict:
                match = [sensor for sensor in self._sensors if address !=
                         "" and address in (sensor.address, sensor.token)]
                if len(match) == 0:  # new sensor
                    logger.debug("Add new sensor '%s' (%s)", name, address)
                    if address.startswith("/") or address.startswith("COM"):
                        self._sensors.append(SerialSensor(self, name, address))
                    else:
                        self._sensors.append(WiFiSensor(self, name, address))
                else:  # rename sensor
                    logger.debug("Rename sensor '%s' to '%s'",
                                 match[0].name, name)
                    match[0].name = name
            elif self._sensorsDict[name].address != "":  # address changed
                logger.debug("Change sensor(%s) address '%s' to '%s'",
                             name, self._sensorsDict[name].address, address)
                self._sensorsDict[name].address = address
            else:  # token changed
                logger.debug("Change sensor(%s) token '%s' to '%s'",
                             name, self._sensorsDict[name].token, address)
                self._sensorsDict[name].token = address

        for name in list(self._sensorsDict.keys()):
            if name not in value:  # deleted sensor
                logger.info("Sensor removed - %s", name)
                self._sensors.remove(self._sensorsDict[name])

    @property
    @type_check
    def _camerasDict(self) -> dict:  # name / Sensor
        """ Gets a dictionary with camera names and Camera. """

        return {camera.name: camera for camera in self._cameras}

    @property
    @type_check
    def cameras(self) -> dict:  # name / address
        """ Gets a dictionary with cameras names and addresses. """

        return {camera.name: camera.address for camera in self._cameras}

    @cameras.setter
    @type_check
    def cameras(self, value: dict) -> None:
        """ Sets the dictionary with cameras names and addresses. """

        for name, address in value.items():
            if name not in self.cameras:
                match = [
                    camera for camera in self._cameras if address == camera.address]
                if len(match) == 0:  # new camera
                    logger.debug("Add new camera '%s' (%s)", name, address)
                    self._cameras.append(Camera(self, name, address))
                else:  # renamed camera
                    logger.debug("Rename camera '%s' to '%s'",
                                 match[0].name, name)
                    match[0].name = name
            else:  # address changed
                logger.debug("Change camera(%s) address '%s' to '%s'",
                             name, self._camerasDict[name].address, address)
                self._camerasDict[name].address = address

        for name in list(self.cameras.keys()):
            if name not in value:  # removed camera
                logger.info("Camera removed - %s", name)
                self._cameras.remove(self._camerasDict[name])

    @property
    @type_check
    def latestData(self) -> dict:
        """ Gets a dictionary with the latest sensors data. """

        result = {}
        for sensor in self._sensors:
            result[sensor.name] = sensor.latestData
        return result

    @property
    @type_check
    def isMotionDetected(self) -> bool:
        """ Gets whether any sensor report motion detection. """

        for sensor in self._sensors:
            if "Motion" in sensor.latestData and sensor.latestData["Motion"]:
                return True
        return False

    @try_catch("Cannot check data", None)
    @type_check
    def _check_data(self, data: list) -> str:
        """Check the set data and fire alarm if necessary.

        Arguments:
            data {list} -- Data list.

        Returns:
            str -- Alert message
        """

        result = []
        for item in data:
            for name, threshold in self.alerts.items():
                if "name" not in item or "value" not in item:
                    continue

                if not (name[0] == "~" and name[1:].strip() in item["name"]) and item["name"] != name:
                    continue

                op = ">"
                if threshold[0] in (">", "<", "="):
                    op = threshold[0]
                    threshold = float(threshold[1:].strip())
                else:
                    threshold = float(threshold.strip())

                if op == ">" and item["value"] > threshold:
                    result.append(item["name"])
                    break
                elif op == "<" and item["value"] < threshold:
                    result.append(item["name"])
                    break
                elif op == "=" and item["value"] == threshold:
                    result.append(item["name"])
                    break
        return ", ".join(result)
