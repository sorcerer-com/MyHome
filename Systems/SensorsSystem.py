import External.serial.tools.list_ports
from External.serial import *
from BaseSystem import *

class SensorsSystem(BaseSystem):
	Name = "Sensors"
	
	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self.sensorNames = ["LivingRoom"]
		self.checkInterval = 15
		self.camerasCount = 1
		self.fireAlarmTempreture = 40
		self.smokeAlarmValue = 40
		
		self._nextTime = datetime.now()
		self._nextTime = self._nextTime.replace(minute=int(self._nextTime.minute / self.checkInterval) * self.checkInterval, second=0, microsecond=0) # the exact self.checkInterval minute in the hour
		self._serials = []
		self._data = []
		self._motion = False
		self._cameras = {}
		
		
	def __del__(self):
		for serial in self._serials:
			serial.close()
		
	def loadSettings(self, configParser, data):
		BaseSystem.loadSettings(self, configParser, data)
		if len(data) ==  0:
			return
		self._data = []
		
		idx = 0
		countSensors = int(data[idx])
		idx += 1
		for i in range(0, countSensors):
			self._data.append({})
			count = int(data[idx])
			idx += 1
			for j in range(0, count):
				key = parse(data[idx], datetime)
				idx += 1				
				values = data[idx].split(";")
				idx+= 1
				self._data[i][key] = tuple([parse(value, None) for value in values])

	def saveSettings(self, configParser, data):
		BaseSystem.saveSettings(self, configParser, data)
		
		data.append(len(self._data))
		for i in range(0, len(self._data)):
			data.append(len(self._data[i]))
			keys = sorted(self._data[i].keys())
			for key in keys:
				data.append(string(key))
				temp = [string(v) for v in self._data[i][key]]
				data.append(";".join(temp))
			
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
			try:
				data = []
				while serial.in_waiting > 0:
					data.append(self._readSerial(serial))
			except Exception as e:
				Logger.log("error", "Sensors System: cannot read from usb port " + serial.port)
				Logger.log("debug", str(e))
				serial.close()
				self._serials.remove(serial)

			if len(data) != 0:
				self._processSerialData(data)
			
		
		if datetime.now() < self._nextTime:
			return
		if self._nextTime.minute % self.checkInterval != 0: # if checkInterval is changed
			self._nextTime = datetime.now()
			self._nextTime = self._nextTime.replace(minute=int(self._nextTime.minute / self.checkInterval) * self.checkInterval, second=0, microsecond=0) # the exact self.checkInterval minute in the hour
			
		# check for new serial device
		self._checkForNewDevice()
		
		# ask for data
		sendCommand = True
		for serial in self._serials:
			try:
				serial.write("getdata")
			except Exception as e:
				Logger.log("error", "Sensors System: cannot send command to " + serial.port)
				Logger.log("debug", str(e))
				serial.close()
				self._serials.remove(serial)
				sendCommand = false
		time.sleep(2) # TODO: may be remove when fix the problem with power supply
			
		# if command is send successfully
		if sendCommand:
			if self._nextTime.minute <= self.checkInterval:
				self._archiveData()
				self._owner.systemChanged = True
			self._nextTime += timedelta(minutes=self.checkInterval)
	
	def _checkForNewDevice(self):
		openedPorts = [serial.port for serial in self._serials]
		ports = list(External.serial.tools.list_ports.comports())
		for p in ports:
			if p.device not in openedPorts and p.name != None and p.name.startswith("ttyUSB") and p.description != "n/a":
				serial = None
				try:
					serial = Serial(p.device, baudrate=9600, timeout=2) # open serial port
					time.sleep(2)
					serial.write("connect")
					id = int(self._readSerial(serial))
					cmd = self._readSerial(serial)
					if id >= 0 and cmd == "connected":
						self._serials.append(serial)
						while len(self._data) <= id:
							self._data.append({})
							self.sensorNames.append("Sensor" + len(self._data))
					else:
						serial.close()
				except Exception as e:
					Logger.log("error", "Sensors System: cannot open usb port " + p.device)
					Logger.log("debug", str(e))
					if serial != None:
						serial.close()
	
	def _processSerialData(self, data):
		print data # TODO: remove
		sensorID = int(data[0])
		if data[1] == "data" and len(data) == 7:
			import math
			humCorrect = round(math.sqrt(float(data[4])) * 10)
			# motion, temperature, humidity, gas value, lighing
			value = (bool(int(data[2])) or self._motion, float(data[3]), humCorrect, float(data[5]) * 100, float(data[6]) * 100)

			lastTime = self._nextTime - timedelta(minutes=self.checkInterval)
			self._data[sensorID][lastTime] = value
			self._motion = bool(int(data[2]))
			self._checkData()
			self._owner.systemChanged = True
		if data[1] == "motion":
			self._motion = True
			
	def _readSerial(self, serial):
		temp = "//"
		while temp.startswith("//"):
			temp = serial.readline().strip()
		return temp
	
	
	def _archiveData(self):
		for i in range(0, len(self._data)):
			keys = sorted(self._data[i].keys())
			idx = 0
			while idx < len(keys):
				times = []
				if (self._nextTime - keys[idx]).days > 365: # for older then 365 days, delete it
					del self._data[i][keys[idx]]
				elif (self._nextTime - keys[idx]).days > 5: # for older then 5 days, save only 1 per day
					for j in range(idx, len(keys)):
						if keys[j].day == keys[idx].day and (keys[j] - keys[idx]).days < 1:
							times.append(keys[j])
						else:
							break
				elif (self._nextTime - keys[idx]).days >= 1: # for older then 24 hour, save only 1 per hour
					for j in range(idx, len(keys)):
						if keys[j].day == keys[idx].day and keys[j].hour == keys[idx].hour and \
							(keys[j] - keys[idx]).seconds < 1 * 3600: # less then hour
							times.append(keys[j])
						else:
							break

				if len(times) > 1:
					values = [self._data[i][t] for t in times]
					values = map(list, zip(*values)) # transpose array
					newValue = []
					for value in values:
						if type(value[0]) is bool:
							newValue.append(len([v for v in value if v]) >= float(len(value)) / 2) # if True values are more then False
						elif type(value[0]) is int:
							newValue.append(int(round(sum(value) / float(len(value)))))
						elif type(value[0]) is float:
							newValue.append(sum(value) / float(len(value)))
					for t in times:
						del self._data[i][t]
					self._data[i][times[0].replace(minute=0, second=0, microsecond=0)] = tuple(newValue)
					idx += len(times)
				else:
					idx += 1
				
	def _checkData(self):
		data = self.getLatestData()
		for key, value in data.items():
			if len(value) == 0:
				continue
			# fire check: if the temperature is higher than set value
			if value[1] > self.fireAlarmTempreture and False: # TODO: remove False when fix the power supply
				self._owner.sendAlert("Fire Alarm Activated!")
				break
			# smoke check: if the smoke value is higher than set value
			if value[3] > self.smokeAlarmValue:
				self._owner.sendAlert("Smoke Alarm Activated!")
				break
				
	def getLatestData(self):
		result = {}
		for i in range(0, len(self._data)):
			keys = sorted(self._data[i].keys())
			if len(keys) > 0:
				result[self.sensorNames[i]] = self._data[i][keys[-1]]
			else:
				result[self.sensorNames[i]] = ()
		return result
		
	def motionDetected(self):
		temp = self._motion
		self._motion = False
		return temp
		
	def getImage(self, cameraIndex=0, size=(640, 480), stamp=True):
		if cameraIndex >= self.camerasCount:
			return None
		
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
			Logger.log("debug", str(e))
			return None
		
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
			Logger.log("debug", str(e))
			return None