import logging
import secrets
from datetime import datetime, timedelta

from Utils.Decorators import try_catch, type_check

logger = logging.getLogger(__name__.split(".")[-1])


class BaseSensor:
    """ BaseSensor class """

    @type_check
    def __init__(self, owner: None, name: str, address: str) -> None:
        """ Initialize an instance of the BaseSensor class.

        Arguments:
            owner {SensorSystem} -- SensorSystem object which is the owner of the sensor.
            name {str} -- Name of the sensor.
            address {str} -- (IP) Address of the sensor.
        """

        self._owner = owner
        self.name = name
        self.address = address
        self.token = secrets.token_hex(16)
        self.metadata = {}  # subName / (type / value)

        self._data = {}  # time / (subName / value)
        self._lastReadings = {}  # subName / value

    @type_check
    def load(self, data: dict) -> None:
        """ Loads sensor's data.

        Arguments:
                data {dict} -- Dictionary from which the sensor data will be loaded.
        """

        logger.debug("Load sensor: %s", self.name)

        if "token" in data:
            self.token = data["token"]
            if self.address == self.token:
                self.address = ""
        if "metadata" in data:
            self.metadata = data["metadata"]
        if "data" in data:
            self._data = data["data"]
        if "lastReadings" in data:
            self._lastReadings = data["lastReadings"]

    @type_check
    def save(self, data: dict) -> None:
        """ Saves sensor's data.

        Arguments:
                data {dict} -- Dictionary to which the sensor data will be saved.
        """

        logger.debug("Save sensor: %s", self.name)

        # Note: address is saved by the config and SensorsSystem's sensors dict
        data["token"] = self.token
        data["metadata"] = self.metadata
        data["data"] = self._data
        data["lastReadings"] = self._lastReadings

    @property
    @type_check
    def latestTime(self) -> datetime:
        """ Gets a latest time data added. """

        if len(self._data) == 0:
            return None

        return sorted(self._data.keys())[-1]

    @property
    @type_check
    def latestData(self) -> dict:
        """ Gets a dictionary with the latest sensor's data. """

        if self.latestTime is None:
            return {}

        return self._data[self.latestTime]

    @property
    @type_check
    def subNames(self) -> list:
        """ Gets a list with all sub names. """

        if self.latestTime is None:
            return []

        return list(self._data[self.latestTime].keys())

    @type_check
    def update(self) -> None:
        """ Update current sensor's state. """
        pass

    @type_check
    def readData(self, time: datetime, addBiggerOnly: bool = False) -> list:
        """ Read and store data from the sensor into data collection.

        Arguments:
            time {datetime} -- Time when the data is collected.
            addBiggerOnly {bool} -- Add only data with bigger value. (default: {False})
        """

        data = self._readData()  # read data from sensor
        logger.debug("Sensor '%s' read data: %s", self.name, data)
        if data is not None:
            self.addData(time, data, addBiggerOnly)
        return data

    @type_check
    def addData(self, time: datetime, data: list, addBiggerOnly: bool = False) -> bool:
        """ Add data to the sensors data collection.

        Arguments:
            time {datetime} -- Time when the data is collected.
            data {list} -- Data which will be added.
            addBiggerOnly {bool} -- Add only data with bigger value. (default: {False})

        Returns:
            bool -- True if data was added, otherwise False.
        """

        if time is None:
            time = datetime.now()

        logger.debug("Sensor '%s' add data at %s: %s (%s)", self.name, time, data, addBiggerOnly)

        result = False
        if time not in self._data:
            self._data[time] = {}
        for item in data:
            if "name" in item and "value" in item:
                aggrType = item["aggrType"] if "aggrType" in item else "avg"
                if aggrType == "avg":
                    if addBiggerOnly and item["name"] in self._data[time] and self._data[time][item["name"]] > item["value"]:
                        # if add only bigger and there is bigger previous value
                        logger.debug("Sensor '%s' skip adding '%s' data with value '%s' smaller than previous '%s'",
                                     self.name, item["name"], item["value"], self._data[time][item["name"]])
                    else:
                        self._data[time][item["name"]] = item["value"]
                        result = True
                else:  # sum type - differentiate
                    prevValue = self._lastReadings[item["name"]
                                                   ] if item["name"] in self._lastReadings else 0
                    if prevValue > item["value"]: # if the new value is smaller than previous - it's reset
                        prevValue = 0
                    self._data[time][item["name"]] = item["value"] - prevValue
                    self._lastReadings[item["name"]] = item["value"]
                    # set real value to return in readData
                    item["value"] -= prevValue
                    result = True
                self.metadata[item["name"]] = {
                    key: value for key, value in item.items() if key not in ("name", "value")}
            else:
                logger.warning(
                    "Try to add invalid data item(%s) in sensor(%s)", item, self.name)

        if result:
            self._owner._owner.event(self, "SensorDataAdded", {
                                    item["name"]: item["value"] for item in data})
            self._archiveData()
        return result

    @type_check
    def _readData(self) -> list:
        """ Abstract method to read data from the sensor. """
        return []

    @try_catch("Cannot archive data")
    @type_check
    def _archiveData(self) -> None:
        """ Archive old data. """

        now = datetime.now()
        # delete entries older then 1 year
        times = [time for time in self._data.keys() if time <
                 now.replace(year=now.year-1)]
        for time in times:
            del self._data[time]

        # for older then 24 hour, save only 1 per day
        times = [time for time in self._data.keys() if time < now.replace(
            hour=0, minute=0, second=0, microsecond=0) - timedelta(days=1)]
        groupedDate = {}
        for time in times:
            if time.date() not in groupedDate:
                groupedDate[time.date()] = []
            groupedDate[time.date()].append(time)

        for date, times in groupedDate.items():
            if len(times) == 1:  # already archived
                continue

            items = [self._data[t] for t in times]
            # delete old records
            for t in times:
                del self._data[t]
            # add one new
            newTime = datetime.combine(date, datetime.min.time())
            self._data[newTime] = {}
            for subName in self.subNames:
                values = [item[subName] for item in items if subName in item]
                if len(values) == 0:
                    continue

                aggrType = "avg"
                if subName in self.metadata and "aggrType" in self.metadata[subName]:
                    aggrType = self.metadata[subName]["aggrType"]

                newValue = None
                if aggrType == "avg":
                    if isinstance(values[0], bool):
                        newValue = len([v for v in values if v]) >= float(
                            len(values)) / 2  # if True values are more then False
                    elif isinstance(values[0], int):
                        newValue = int(round(sum(values) / float(len(values))))
                    elif isinstance(values[0], float):
                        newValue = sum(values) / float(len(values))
                elif aggrType == "sum":
                    if isinstance(values[0], bool):
                        newValue = len([v for v in values if v]) > 0
                    else:
                        newValue = sum(values)
                self._data[newTime][subName] = newValue
