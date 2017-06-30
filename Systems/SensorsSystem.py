from BaseSystem import *
try:
	import RPi.GPIO as GPIO
except:
	Logger.log("warning", "Sensors System: the device isn't Raspberry Pi and Sensors are not supported")

class SensorsSystem(BaseSystem):
	Name = "Sensors"
	
	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self.sensorTypes = ["Motion", "TempHum"]
		self.sensorPins = ["7", "4"]
		self.sensorNames = ["LivingRoom", "LivingRoom"]
		self.checkInterval = 15
		self.camerasCount = 1
		self.fireAlarmTempreture = 50
		
		self._init = self._initSensors()
		self._nextTime = datetime.now()
		self._nextTime = self._nextTime.replace(minute=int(self._nextTime.minute / self.checkInterval) * self.checkInterval, second=0, microsecond=0) # the exact self.checkInterval minute in the hour
		self._data = {}
		self._cameras = {}
		
	def loadSettings(self, configParser, data):
		BaseSystem.loadSettings(self, configParser, data)
		if self._init:
			self._initSensors()
		if len(data) ==  0:
			return
		self._data = {}
		
		count = int(data[0])
		for i in range(0, count):
			key = parse(data[1 + i * 2], datetime)
			self._data[key] = []
			values = data[2 + i * 2].split(";")
			for value in values:
				self._data[key].append(parse(value, None))

	def saveSettings(self, configParser, data):
		BaseSystem.saveSettings(self, configParser, data)
		
		data.append(len(self._data))
		keys = sorted(self._data.keys())
		for key in keys:
			data.append(string(key))
			temp = [string(v) for v in self._data[key]]
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
				
		if not self._init or datetime.now() < self._nextTime:
			return
		if self._nextTime.minute % self.checkInterval != 0: # if checkInterval is changed
			self._nextTime = datetime.now()
			self._nextTime = self._nextTime.replace(minute=int(self._nextTime.minute / self.checkInterval) * self.checkInterval, second=0, microsecond=0) # the exact self.checkInterval minute in the hour
		
		if self._nextTime not in self._data:
			self._data[self._nextTime] = []
		for i in range(0, len(self.sensorTypes)):
			if len(self._data[self._nextTime]) <= i:
				self._data[self._nextTime].append(None)
			if self._data[self._nextTime][i] is not None: # if there is already data there
				continue
				
			pin = int(self.sensorPins[i])
			if self.sensorTypes[i] == "Motion":
				self._data[self._nextTime][i] = self._detectMotion(pin)
			elif self.sensorTypes[i] == "TempHum":
				self._data[self._nextTime][i] = self._getTemperatureAndHumidity(pin)
		
		# if data from all sensors is collected
		if None not in self._data[self._nextTime]:
			if self._nextTime.minute <= self.checkInterval:
				self._archiveData()
			self._checkData()
			self._owner.systemChanged = True
			self._nextTime += timedelta(minutes=self.checkInterval)
			
	def _archiveData(self):
		keys = sorted(self._data.keys())
		idx = 0
		while idx < len(keys):
			times = []
			if (self._nextTime - keys[idx]).days > 365: # for older then 365 days, delete it
				del self._data[keys[idx]]
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
				values = [self._data[t] for t in times if self._data[t] is not None and None not in self._data[t]]
				values = map(list, zip(*values)) # transpose array
				newValue = []
				for i in range(0, len(values)):
					if self.sensorTypes[i] == "Motion":
						newValue.append(len([v for v in values[i] if v]) >= float(len(values[i])) / 2) # if True values are more then False
					elif self.sensorTypes[i] == "TempHum":
						newValue.append(tuple([int(round(sum(v) / float(len(values[i])))) for v in zip(*values[i])])) # avarage value for the tuple
				for t in times:
					del self._data[t]
				self._data[times[0].replace(minute=0, second=0, microsecond=0)] = newValue
				idx += len(times)
			else:
				idx += 1
				
	def _checkData(self):
		data = self.getLatestData()
		for i in range(0, len(self.sensorTypes)):
			# fire check: if some temperature is higher than set value
			if self.sensorTypes[i] == "TempHum" and data[i][0] > self.fireAlarmTempreture:
				self._owner.sendAlert("Fire Alarm Activated!")
				break
				
	def getLatestData(self):
		if len(self._data) == 0:
			return None
			
		keys = sorted(self._data.keys())
		return self._data[keys[-1]]
		
	def _initSensors(self):
		try:
			GPIO.setwarnings(False)
			GPIO.cleanup()
			GPIO.setwarnings(True)
			GPIO.setmode(GPIO.BCM)
			for i in range(0, len(self.sensorTypes)):
				pin = int(self.sensorPins[i])
				if self.sensorTypes[i] == "Motion":
					GPIO.setup(pin, GPIO.IN)
					GPIO.add_event_detect(pin, GPIO.RISING)
			return True
		except:
			return False

	def countSensors(self, type):
		count = 0
		for sensorType in self.sensorTypes:
			if sensorType == type:
				count += 1
		return count
		
			
	def detectMotion(self, index):
		pin = [self.sensorPins[i] for i in range(0, len(self.sensorTypes)) if self.sensorTypes[i] == "Motion"][index]
		return self._detectMotion(int(pin)) == True
		
	def _detectMotion(self, pin):
		try:
			if GPIO.event_detected(pin):
				Logger.log("info", "Sensors System: motion detected")
				return True
			return False
		except Exception as e:
			Logger.log("error", "Sensors System: cannot detect motion")
			Logger.log("debug", str(e))
			return None

	def detectAnyMotion(self):
		count = self.countSensors("Motion")
		for i in range(0, count):
			if self.detectMotion(i):
				return True
		return False
		
	
	def getTemperatureAndHumidity(self, index):
		pin = [self.sensorPins[i] for i in range(0, len(self.sensorTypes)) if self.sensorTypes[i] == "Motion"][index]
		value = self._getTemperatureAndHumidity(int(pin))
		if value == None and len(self._data) > 0:
			value = self._data[sorted(self._data.keys())[-1]][index]
		return value

	# https://github.com/netikras/r-pi_DHT11
	def _getTemperatureAndHumidity(self, pin):
		def bin2dec(string_num):
			return int(string_num, 2)
		
		try:
			data = []
			effectiveData = []
			
			GPIO.setup(pin,GPIO.OUT)
			GPIO.output(pin,GPIO.HIGH)
			time.sleep(0.025)
			GPIO.output(pin,GPIO.LOW)
			time.sleep(0.14)
			
			GPIO.setup(pin, GPIO.IN, pull_up_down=GPIO.PUD_UP)
			for i in range(0,1000):
				data.append(GPIO.input(pin))
		
			seek=0;
			bits_min=9999;
			bits_max=0;
			HumidityBit = ""
			TemperatureBit = ""
			crc = ""

			while(seek < len(data) and data[seek] == 0):
				seek+=1;
			
			while(seek < len(data) and data[seek] == 1):
				seek+=1;

			for i in range(0, 40):
				buffer = "";
				while(seek < len(data) and data[seek] == 0):
					seek+=1;
				while(seek < len(data) and data[seek] == 1):
					seek+=1;
					buffer += "1";
				
				if (len(buffer) < bits_min):
					bits_min = len(buffer)
				if (len(buffer) > bits_max):
					bits_max = len(buffer)
				
				effectiveData.append(buffer);

			for i in range(0, len(effectiveData)):
				if (len(effectiveData[i]) < ((bits_max + bits_min)/2)):
					effectiveData[i] = "0";
				else:
					effectiveData[i] = "1";

			for i in range(0, 8):
				HumidityBit += str(effectiveData[i]);
			
			for i in range(16, 24):
				TemperatureBit += str(effectiveData[i]);
    
			for i in range(32, 40):
				crc += str(effectiveData[i]);
    
			Humidity = bin2dec(HumidityBit)
			Temperature = bin2dec(TemperatureBit)

			if Humidity + Temperature == bin2dec(crc) and bin2dec(crc) != 0:
				import math
				humCorrect = int(round(math.sqrt(Humidity) * 10))
				return (Temperature, humCorrect)
			else:
				return None
		except Exception as e:
			Logger.log("error", "Sensors System: cannot get temperature and humidity")
			Logger.log("debug", str(e))
			return None

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