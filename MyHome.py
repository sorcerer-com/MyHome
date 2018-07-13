import json, time, threading, os, ConfigParser
from datetime import datetime, timedelta

from Utils.Logger import Logger
from Utils.Config import Config
from Utils.Utils import parse, string
from Utils.Event import Event
from Services.PCControlService import PCControlService
from Services.InternetService import InternetService
from Systems.SecuritySystem import SecuritySystem
from Systems.ScheduleSystem import ScheduleSystem
from Systems.MediaPlayerSystem import MediaPlayerSystem
from Systems.ControlSystem import ControlSystem
from Systems.AISystem import AISystem
from Systems.SensorsSystem import SensorsSystem

class MHome(object):
	Name = "MyHome"
	_UpdateTime = 0.1

	def __init__(self):
		Logger.log("info", "Start My Home")
		
		self.event = Event()
		
		# systems
		self.systems = {}
		self.systems[SecuritySystem.Name] = SecuritySystem(self)
		self.systems[ScheduleSystem.Name] = ScheduleSystem(self)
		self.systems[MediaPlayerSystem.Name] = MediaPlayerSystem(self)
		self.systems[ControlSystem.Name] = ControlSystem(self)
		self.systems[AISystem.Name] = AISystem(self)
		self.systems[SensorsSystem.Name] = SensorsSystem(self)
		self.systemChanged = False
		
		self._lastBackupSettings = datetime.now()
		self.loadSettings()
		
		t = threading.Thread(target=self.update)
		t.daemon = True
		t.start()
		
	def __del__(self):
		Logger.log("info", "Stop My Home")
		MHome._UpdateTime = 0
		self.saveSettings()
		for key in self.systems.keys():
			del self.systems[key]

	def update(self):
		while(MHome._UpdateTime > 0):
			#start = datetime.now()
			# update systems
			for system in self.systems.values():
				if system.enabled:
					system.update()
					
			if self.systemChanged:
				self.saveSettings()
				self.systemChanged = False
			#Logger.log("info", str(datetime.now() - start))
			
			time.sleep(MHome._UpdateTime)
			
	def sendAlert(self, msg):
		Logger.log("info", "My Home: send alert '%s'" % msg)
		PCControlService.captureImage("test.jpg", "640x480", 1, 4)
		msg = time.strftime("%d/%m/%Y %H:%M:%S") + "\n" + msg
		msg += "\n%s" % self.systems[SensorsSystem.Name].getLatestData()
		InternetService.sendSMS(Config.GSMNumber, "telenor", msg)
		InternetService.sendEMail([Config.EMail], "My Home", msg, ["test.jpg"])
		if os.path.isfile("test.jpg"):
			os.remove("test.jpg")

	def loadSettings(self):
		Logger.log("info", "My Home: load settings")
		configParser = ConfigParser.RawConfigParser()
		configParser.optionxform = str
		if Config.ConfigFileName not in configParser.read(Config.ConfigFileName):
			return
			
		data = []
		if os.path.isfile(Config.DataFileName):
			with open(Config.DataFileName, 'r') as f:
				data = json.load(f)
			self._lastBackupSettings = parse(data["0"], datetime)
			
		Config.load(configParser)
		
		# load systems settings
		keys = sorted(self.systems.keys())
		for key in keys:
			self.systems[key].loadSettings(configParser, data[key])
		
		self.systemChanged = False
		self.event(self, "SettingsLoaded")
		
	def saveSettings(self):
		Logger.log("info", "My Home: save settings")
		# backup config and data file every day
		if (datetime.now() - self._lastBackupSettings) > timedelta(days=1):
			from shutil import copyfile
			self._lastBackupSettings = datetime.now()
			copyfile(Config.ConfigFileName, Config.ConfigFileName + ".bak")
			copyfile(Config.DataFileName, Config.DataFileName + ".bak")
		
		configParser = ConfigParser.RawConfigParser()
		configParser.optionxform = str
		
		Config.save(configParser)
		
		# save systems settings
		data = {}
		data[0] = string(self._lastBackupSettings)
		keys = sorted(self.systems.keys())
		for key in keys:
			configParser.add_section(self.systems[key].Name)
			systemData = {}
			self.systems[key].saveSettings(configParser, systemData)
			data[self.systems[key].Name] = systemData
		data = json.dumps(data, indent=4, sort_keys=True, ensure_ascii=False).encode('utf8')
				
		with open(Config.ConfigFileName, 'wb') as configFile:
			configParser.write(configFile)
			
		with open(Config.DataFileName, 'w') as dataFile:
			dataFile.write(data)
		
		self.event(self, "SettingsSaved")
