import time
from datetime import datetime, timedelta

import External.serial.tools.list_ports
from External.serial import Serial

from Utils.Logger import Logger
from Utils.Utils import parse, string
from BaseSystem import BaseSystem
from Systems.Models.Sensor import Sensor
from Services.InternetService import InternetService


class SensorsSystem(BaseSystem):
	Name = "Sensors"
	
	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self.checkInterval = 15
		self.camerasCount = 1
		self.sensorsIPs = ["192.168.0.105"]
		self.powerCycleDay = 6
		self.powerDayTariffHour = 6
		self.powerDayTariffPrice = (0.13294 + 0.04748) * 1.2
		self.powerNightTariffHour = 23
		self.powerNightTariffPrice = (0.05654 + 0.04748) * 1.2
		self.fireAlarmTempreture = 40
		self.smokeAlarmValue = 50
		self.powerAlarmValue = 3000
		self.consumedPowerAlarmValue = 1000
		
		self._nextTime = datetime.now()
		self._nextTime = self._nextTime.replace(minute=int(self._nextTime.minute / self.checkInterval) * self.checkInterval, second=50, microsecond=0) # in the middle of the  self.checkInterval minute in the hour
		self._serials = []
		self._sensors = {} # id / sensor
		self._motion = False
		self._cameras = {}
		
		self._lastPowerReadings = []
		
	def __del__(self):
		for serial in self._serials:
			serial.close()
	
	
	@property
	def sensorNames(self):
		return [sensor.Name for sensor in self._sensors.values()]
		
	@sensorNames.setter
	def sensorNames(self, value):
		for i in range(len(value)):
			if len(self._sensors) <= i:
				self._sensors[i] = Sensor(value[i])
			else:
				self._sensors.values()[i].Name = value[i]
	
		
	def loadSettings(self, configParser, data):
		BaseSystem.loadSettings(self, configParser, data)
		if len(data) ==  0:
			return
		
		# rearange sensors by ids in the dictionary
		self._sensors.clear()
		for (key, value) in data["ids"].items():
			self._sensors[int(value)] = Sensor(str(key))
		self._lastPowerReadings = data["lastPowerReadings"]
		
		for id in data["ids"].values():
			for (time, values) in data[str(id)].items():
				if time == "subNames":
					continue
				for i in range(0, len(values)):
					name = data[str(id)]["subNames"][i]
					self._sensors[id].addValue(parse(time, datetime), str(name), values[i])

	def saveSettings(self, configParser, data):
		BaseSystem.saveSettings(self, configParser, data)

		data["ids"] = {sensor.Name: id for (id, sensor) in self._sensors.items()}
		data["lastPowerReadings"] = self._lastPowerReadings
		# TODO: maybe move to Sensor.py
		for (id, sensor) in self._sensors.items():
			data[id] = {}
			data[id]["subNames"] = sensor.subNames
			times = sorted(sensor._data.values()[0].keys())
			for time in times:
				data[id][string(time)] = []
				for subName in sensor.subNames:
					data[id][string(time)].append(sensor._data[subName][time])
			
	def update(self):
		BaseSystem.update(self)
		
		# clean cameras
		if len(self._cameras) > 0: 
			for key in self._cameras.keys():
				if datetime.now() - self._cameras[key][1] > timedelta(minutes=5):
					self._cameras[key][0].stop()
					del self._cameras[key]
					Logger.log("info", "Sensors System: stop camera %d" % key)
					
		
		# read from serial
		for serial in self._serials:
			data = self._readDataSerial(serial)
			if data == None:
				serial.close()
				self._serials.remove(serial)
			elif len(data) != 0:
				self._processSerialData(data)

		# check for new serial device
		delta = self._nextTime - datetime.now()
		if delta.seconds % 10 == 0 and delta.microseconds < 100000:
			self._checkForNewDevice()
			
		
		if datetime.now() < self._nextTime:
			return
		if self._nextTime.minute % self.checkInterval != 0: # if checkInterval is changed
			self._nextTime = datetime.now()
			self._nextTime = self._nextTime.replace(minute=int(self._nextTime.minute / self.checkInterval) * self.checkInterval, second=50, microsecond=0) # in the middle of the self.checkInterval minute in the hour
			
		# TODO: fix to work with more then one sensor
		json = InternetService.getJsonContent("http://%s/data" % self.sensorsIPs[0])
		if json != None:
			for i in range(0, 3):
				try:
					self._sensors[1].addValue(self._nextTime, "Power" + str(i+1), float(json[i * 2]["value"]))
					self._checkData("Power" + str(i+1), float(json[i * 2]["value"]))
					if i >= len(self._lastPowerReadings):
						self._lastPowerReadings.append(float(json[i * 2 + 1]["value"]))
					value = float(json[i * 2 + 1]["value"]) - self._lastPowerReadings[i]
					if value < 0:
						value = 0
					self._sensors[1].addValue(self._nextTime, "ConsumedPower" + str(i+1), value)
					self._checkData("ConsumedPower" + str(i+1), value)
					if value >= 0:
						self._lastPowerReadings[i] = float(json[i * 2 + 1]["value"])
				except Exception as e:
					Logger.log("error", "Sensors System: cannot parse json " + str(json))
					Logger.log("exception", str(e))
			self._owner.systemChanged = True

		# TODO: if doesn't receive data from all sensors maybe send command again (may be to specific sensorID)
		
		# ask for data
		sendCommand = True
		for serial in self._serials:
			if not self._writeSerial(serial, "getdata"):
				serial.close()
				self._serials.remove(serial)
				sendCommand = False
			
		# if command is send successfully
		if sendCommand:
			if self._nextTime.minute < self.checkInterval:
				for sensor in self._sensors.values():
					sensor.archiveData()
				self._owner.systemChanged = True
			self._nextTime += timedelta(minutes=self.checkInterval)
	
	
	def _checkForNewDevice(self):
		openedPorts = [serial.port for serial in self._serials]
		ports = list(External.serial.tools.list_ports.comports())
		# TODO: if device isn't "sensor" and pass the if createria then it will delay every time
		for p in ports:
			if p.device not in openedPorts and p.name != None and \
				p.name.startswith("ttyUSB") and p.description != "n/a":
				serial = None
				try:
					serial = Serial(p.device, baudrate=9600, timeout=2) # open serial port
					time.sleep(2)
					serial.write("connect")
					id = int(self._readSerial(serial)) - 1
					cmd = self._readSerial(serial)
					if id >= 0 and cmd == "connected":
						self._serials.append(serial)
						if id not in self._sensors:
							self._sensors[id] = Sensor("Sensor" + str(id))
					else:
						serial.close()
				except Exception as e:
					Logger.log("error", "Sensors System: cannot open usb port " + p.device)
					Logger.log("exception", str(e))
					if serial != None:
						serial.close()
	
	def _processSerialData(self, data):
		# fix NANs
		for i in range(0, len(data)):
			if data[i] == "nan":
				data[i] = "0.0"
		
		try:
			sensorId = int(data[0]) - 1
			if data[1] == "data":
				lastTime = self._nextTime - timedelta(minutes=self.checkInterval)
				for i in range(2, len(data) - 1, 2):
					value = parse(data[i + 1], None)
					if data[i] == "Motion":
						value = value or self._motion
						self._motion = False
					self._sensors[sensorId].addValue(lastTime, data[i], value)
					self._checkData(data[i], value)
				self._owner.systemChanged = True
					
			if data[1] == "motion":
				self._motion = True
		except Exception as e:
			Logger.log("error", "Sensors System: cannot process serial data: " + str(data))
			Logger.log("exception", str(e))
			
	def _readSerial(self, serial):
		temp = "//"
		while temp.startswith("//"):
			temp = serial.readline().strip()
		return temp
		
	def _readDataSerial(self, serial):
		try:
			data = []
			if serial.in_waiting > 0:
				while len(data) == 0 or data[-1] != "end":
					data.append(self._readSerial(serial))
					if data[-1] == "":
						del data[-1]
						break
			return data
		except Exception as e:
			Logger.log("error", "Sensors System: cannot read from usb port " + serial.port)
			Logger.log("exception", str(e))
			return None
			
	def _writeSerial(self, serial, value):
		try:
			serial.write(str(value))
			return True
		except Exception as e:
			Logger.log("error", "Sensors System: cannot send command to " + serial.port)
			Logger.log("exception", str(e))
			return False
	
	def _checkData(self, name, value):
		# TODO: maybe as dictionary - sensor name / alarm value
		# fire check: if the temperature is higher than set value
		if name == "Temperature" and value > self.fireAlarmTempreture:
			self._owner.sendAlert("Fire Alarm Activated!")
		# smoke check: if the smoke value is higher than set value
		elif name == "Smoke" and value > self.smokeAlarmValue:
			self._owner.sendAlert("Smoke Alarm Activated!")
		# power check: if the current power consumption is higher than set value
		elif name.startswith("Power") and value > self.powerAlarmValue:
			self._owner.sendAlert("Power Alarm Activated!")
		# power check: if the last consumed power is higher than set value
		elif name.startswith("ConsumedPower") and value > self.consumedPowerAlarmValue:
			self._owner.sendAlert("Consumed Power Alarm Activated!")
	
	
	def getLatestData(self):
		result = {}
		for sensor in self._sensors.values():
			result[sensor.Name] = sensor.getLatestData()
		return result
		
	def isMotionDetected(self):
		temp = self._motion
		self._motion = False
		return temp
		
	def getMonthlyPowerConsumption(self): # [(day,night,total,price), (day,night,total,price)] - current, last month
		cycleDate = datetime.now()
		if cycleDate.day < self.powerCycleDay:
			if cycleDate.month > 1:
				cycleDate = cycleDate.replace(month=cycleDate.month-1)
			else:
				cycleDate = cycleDate.replace(month=12, year=cycleDate.year-1)
		cycleDate = cycleDate.replace(day=self.powerCycleDay, hour=0, minute=0, second=0, microsecond=0)
		prevCycleDate = cycleDate
		if prevCycleDate.month > 1:
			prevCycleDate = prevCycleDate.replace(month=prevCycleDate.month-1)
		else:
			prevCycleDate = prevCycleDate.replace(month=12, year=prevCycleDate.year-1)

		result = []
		result.append(self._getPowerConsumption(cycleDate, datetime.now()))
		result.append(self._getPowerConsumption(prevCycleDate, cycleDate))
		return result
		
	def _getPowerConsumption(self, startDate, endDate):
		day = 0.0
		night = 0.0
		for sensor in self._sensors.values():
			for subName in sensor.subNames:
				if subName.startswith("ConsumedPower"):
					data = sensor.getData(subName, startDate, endDate)
					times = sorted(data.keys())
					for i in range(1, len(times)):
						if times[i].hour > self.powerDayTariffHour and times[i].hour < self.powerNightTariffHour:
							day += data[times[i]]
						else:
							night += data[times[i]]
		total = round(day + night, 2)
		price = round(day / 1000 * self.powerDayTariffPrice + night / 1000 * self.powerNightTariffPrice, 2)
		return (round(day, 2), round(night, 2), total, price)
		
	def getImage(self, cameraIndex=0, size=(640, 480), stamp=True):
		if cameraIndex >= self.camerasCount:
			return None
		
		# init camera
		try:
			from SimpleCV import Camera
			if cameraIndex not in self._cameras:
				self._cameras[cameraIndex] = [Camera(camera_index=cameraIndex, threaded=False), datetime.now()]
				if not hasattr(self._cameras[cameraIndex][0], "threaded"): # check if camera is really created
					del self._cameras[cameraIndex]
					return None
				Logger.log("info", "Sensors System: init camera %d" % cameraIndex)
		except Exception as e:
			Logger.log("warning", "Sensors System: cannot init cameras")
			Logger.log("exception", str(e))
			return None
		
		# capture image
		try:
			img = self._cameras[cameraIndex][0].getImage()
			self._cameras[cameraIndex][1] = datetime.now()
			img = img.resize(size[0], size[1])
			if stamp:
				from SimpleCV import Color
				img.drawText(time.strftime("%d/%m/%Y %H:%M:%S"), 5, 5, Color.WHITE)
			return img
		except Exception as e:
			Logger.log("error", "Sensors System: cannot get image")
			Logger.log("exception", str(e))
			return None
