from threading import Timer
from Utils.Logger import *
from Utils.Event import *
from Systems.SecuritySystem import *
from Systems.ScheduleSystem import *
from Systems.MediaPlayerSystem import *
from Systems.ControlSystem import *

class MHome(object):
	updateTime = 0.1

	def __init__(self):
		Logger.log("info", "Start My Home")
		
		# systems
		self.systems = {}
		self.systems[SecuritySystem.Name] = SecuritySystem(self)
		self.systems[ScheduleSystem.Name] = ScheduleSystem(self)
		self.systems[MediaPlayerSystem.Name] = MediaPlayerSystem(self)
		self.systems[ControlSystem.Name] = ControlSystem(self)
		self.systemChanged = False
		
		self.event = Event()
		
		self.loadSettings()
		self.update()
		
	def __del__(self):
		Logger.log("info", "Stop My Home")
		MHome.updateTime = 0
		self.saveSettings();

	def update(self):				
		# update systems
		for system in self.systems.values():
			if system.enabled:
				system.update()
				
		if self.systemChanged:
			self.saveSettings();
			self.systemChanged = False
				
		if MHome.updateTime > 0:
			Timer(MHome.updateTime, self.update).start()
			
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
			
		Config.load(configParser)
		
		# load systems settings
		for (key, system) in self.systems.items():
			system.loadSettings(configParser)
		
		self.systemChanged = False
		self.event(self, "SettingsLoaded")
		
	def saveSettings(self):
		Logger.log("info", "My Home: save settings")
		configParser = ConfigParser.RawConfigParser()
		configParser.optionxform = str
		
		Config.save(configParser)
		
		# save systems settings
		for (key, system) in self.systems.items():
			system.saveSettings(configParser)
				
		with open(Config.ConfigFileName, 'wb') as configfile:
			configParser.write(configfile)
		
		self.event(self, "SettingsSaved")
