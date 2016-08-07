from threading import Timer
from Utils.Logger import *
from Utils.Event import *
from Systems.SecuritySystem import *
from Systems.ScheduleSystem import *
from Systems.MediaPlayerSystem import *
from Systems.ControlSystem import *
from Systems.AISystem import *

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
		self.systemChanged = False
		
		self.loadSettings()
		self.update()
		
	def __del__(self):
		Logger.log("info", "Stop My Home")
		MHome._UpdateTime = 0
		self.saveSettings();

	def update(self):				
		# update systems
		for system in self.systems.values():
			if system.enabled:
				system.update()
				
		if self.systemChanged:
			self.saveSettings();
			self.systemChanged = False
				
		if MHome._UpdateTime > 0:
			Timer(MHome._UpdateTime, self.update).start()
			
	def test(self):
		Logger.log("info", "My Home: test")
		PCControlService.captureImage("test.jpg", "640x480", 1, 4)
		InternetService.sendSMS(Config.GSMNumber, "Test", "telenor")
		InternetService.sendEMail([Config.EMail], "My Home", "Test", ["test.jpg"])
		if os.path.isfile("test.jpg"):
			os.remove("test.jpg")

	def loadSettings(self):
		Logger.log("info", "My Home: load settings")
		configParser = ConfigParser.RawConfigParser()
		configParser.optionxform = str
		if Config.ConfigFileName not in configParser.read(Config.ConfigFileName):
			return
			
		data = []
		with open(Config.DataFileName, 'r') as f:
			data = f.read().split("\n")
			
		Config.load(configParser)
		
		# load systems settings
		for (key, system) in self.systems.items():
			if ("[%s]" % key) in data:
				systemData = data[data.index("[%s]" % key)+1:]
				for i in range(1, len(systemData)):
					if systemData[i].startswith("["):
						systemData = systemData[:i]
						break
						
			system.loadSettings(configParser, systemData)
		
		self.systemChanged = False
		self.event(self, "SettingsLoaded")
		
	def saveSettings(self):
		Logger.log("info", "My Home: save settings")
		configParser = ConfigParser.RawConfigParser()
		configParser.optionxform = str
		
		Config.save(configParser)
		
		# save systems settings
		data = []
		for (key, system) in self.systems.items():
			configParser.add_section(system.Name)
			systemData = []
			system.saveSettings(configParser, systemData)

			data.append("[%s]" % system.Name)
			data.extend(systemData)
			data.append("")
				
		with open(Config.ConfigFileName, 'wb') as configFile:
			configParser.write(configFile)
			
		with open(Config.DataFileName, 'w') as dataFile:
			for item in data:
				dataFile.write("%s\n" % item)
		
		self.event(self, "SettingsSaved")
