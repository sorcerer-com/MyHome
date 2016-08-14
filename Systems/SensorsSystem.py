import RPi.GPIO as GPIO
from BaseSystem import *

class SensorsSystem(BaseSystem):
	Name = "Sensors"
	
	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self.sensorTypes = ["Motion", "TempHum"]
		self.sensorPins = ["7", "4"]
		self.checkInterval = 15
		
		GPIO.setwarnings(False)
		self._init = self._initSensors()
		GPIO.setwarnings(True)
		self._nextTime = datetime.now()
		self._nextTime = self._nextTime.replace(minute=int(self._nextTime.minute / self.checkInterval) * self.checkInterval, second=0, microsecond=0) # the exact self.checkInterval minute in the hour
		self._data = {}
		
	def loadSettings(self, configParser, data):
		BaseSystem.loadSettings(self, configParser, data)
		if self._init:
			self._initSensors()
		
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
			if not self._removeLastData():
				self._owner.systemChanged = True
			self._nextTime += timedelta(minutes=self.checkInterval)
			
	def _removeLastData(self):
		prevTime = None
		if len(self._data) > 1:
			prevTime = sorted(self._data.keys())[-2]
		# if the last data is the same with previous one delete it
		if prevTime in self._data:
			remove = True
			for i in range(0, len(self._data[self._nextTime])):
				if self.sensorTypes[i] == "TempHum":
					# if temperature diff is more then 1 or humidity diff is more then 5
					if abs(self._data[self._nextTime][i][0] - self._data[prevTime][i][0]) > 1 or \
						abs(self._data[self._nextTime][i][1] - self._data[prevTime][i][1]) > 5:
						remove = False
				else:
					remove = self._data[self._nextTime] == self._data[self._nextTime]
				if remove == False:
					break
			
			if remove:
				del self._data[self._nextTime]
				return True
		return False
	
	def _initSensors(self):
		try:
			GPIO.cleanup()
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
			return str(int(string_num, 2))
		
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

			if int(Humidity) + int(Temperature) == int(bin2dec(crc)):
				return (int(Temperature), int(Humidity))
			else:
				return None
		except Exception as e:
			Logger.log("error", "Sensors System: cannot get temperature and humidity")
			Logger.log("debug", str(e))
			return None
