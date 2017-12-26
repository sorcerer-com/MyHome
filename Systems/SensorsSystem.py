import External.serial.tools.list_ports
from External.serial import *
from BaseSystem import *
from Systems.Models.Sensor import *

class SensorsSystem(BaseSystem):
	Name = "Sensors"
	
	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self.checkInterval = 15
		self.camerasCount = 1
		self.fireAlarmTempreture = 40
		self.smokeAlarmValue = 50
		
		self._nextTime = datetime.now()
		self._nextTime = self._nextTime.replace(minute=int(self._nextTime.minute / self.checkInterval) * self.checkInterval, second=0, microsecond=0) # the exact self.checkInterval minute in the hour
		self._serials = []
		self._sensors = {} # id / sensor
		self._motion = False
		self._cameras = {}
		
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
		idx = 0
		ids = data[idx].split(";")
		idx += 1
		names = self.sensorNames
		self._sensors.clear()
		for i in range(len(ids)):
			self._sensors[int(ids[i])] = Sensor(names[i])
		
		countSensors = int(data[idx])
		idx += 1
		for i in range(0, countSensors):
			id = int(data[idx])
			idx += 1
			subNames = data[idx].split(";")
			idx+= 1
			count = int(data[idx])
			idx += 1
			for j in range(0, count):
				key = parse(data[idx], datetime)
				idx += 1				
				values = data[idx].split(";")
				idx+= 1
				for k in range(len(values)):
					self._sensors[id].addValue(key, str(subNames[k]), parse(values[k], None))

	def saveSettings(self, configParser, data):
		BaseSystem.saveSettings(self, configParser, data)

		temp = {sensor.Name: id for (id, sensor) in self._sensors.items()}
		ids = [str(temp[name]) for name in self.sensorNames]
		data.append(";".join(ids))
		
		data.append(len(self._sensors))
		for (id, sensor) in self._sensors.items():
			data.append(id)
			data.append(";".join(sensor.subNames))
			times = sorted(sensor._data.values()[0].keys())
			data.append(len(times))
			for time in times:
				data.append(string(time))
				values = [string(sensor._data[subName][time]) for subName in sensor.subNames]
				data.append(";".join(values))
			
	def update(self):
		BaseSystem.update(self)
		
		# clean cameras
		if len(self._cameras) > 0: 
			for key in self._cameras.keys():
				if datetime.now() - self._cameras[key][1] > timedelta(minutes=5):
					self._cameras[key][0].stop()
					del self._cameras[key]
					Logger.log("info", "Sensors System: stop camera %d" % key);
					
		
		# read from serial
		for serial in self._serials:
			data = self._readDataSerial(serial)
			if data == None:
				serial.close()
				self._serials.remove(serial)
			elif len(data) != 0:
				self._processSerialData(data)
			
		
		if datetime.now() < self._nextTime:
			return
		if self._nextTime.minute % self.checkInterval != 0: # if checkInterval is changed
			self._nextTime = datetime.now()
			self._nextTime = self._nextTime.replace(minute=int(self._nextTime.minute / self.checkInterval) * self.checkInterval, second=0, microsecond=0) # the exact self.checkInterval minute in the hour
			
		# check for new serial device
		self._checkForNewDevice() # TODO: may be do it more often (not in 15 minutes)
		# TODO: if doesn't receive data from all sensors maybe send command again (may be to specific sensorID)
		# TODO: property wrapper
		
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
				import math

				lastTime = self._nextTime - timedelta(minutes=self.checkInterval)
				for i in range(2, len(data) - 1, 2):
					value = parse(data[i + 1], None)
					self._sensors[sensorId].addValue(lastTime, data[i], value)
					self._checkData(data[i], value)
				self._owner.systemChanged = True
					
			if data[1] == "motion":
				self._motion = True
		except Exception as e:
			Logger.log("error", "Sensors System: process serial data: " + str(data))
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
	
	
	def getLatestData(self):
		result = {}
		for sensor in self._sensors.values():
			result[sensor.Name] = sensor.getLatestData()
		return result
		
	def motionDetected(self):
		temp = self._motion
		self._motion = False
		for sensor in self._sensors.values():
			if "Motion" in sensor.subNames and sensor.getLatestValue("Motion"):
				return True
		return temp
		
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
				Logger.log("info", "Sensors System: init camera %d" % cameraIndex);
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