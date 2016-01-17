from threading import Timer
from Utils.Utils import *
from Utils.Logger import *
from Systems.SecuritySystem import *
from Systems.ScheduleSystem import *

# TODO: may be implement one day(month) tests - check if everything is ok
class MHome():
	updateTime = 0.1

	def __init__(self):
		Logger.log("info", "Start My Home")
		
		# systems
		self.systems = {}
		self.systems[SecuritySystem.Name] = SecuritySystem(self)
		self.systems[ScheduleSystem.Name] = ScheduleSystem(self)
		
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
				
		if MHome.updateTime > 0:
			Timer(MHome.updateTime, self.update).start()

	def loadSettings(self):
		configParser = ConfigParser.RawConfigParser()
		configParser.optionxform = str
		if Config.ConfigFileName not in configParser.read(Config.ConfigFileName):
			return
			
		Config.load(configParser)
		
		# load systems settings
		for (key, system) in self.systems.items():
			items = configParser.items(key)
			for (name, value) in items:
				if hasattr(system, name):
					propType = type(getattr(system, name))
					setattr(system, name, parse(value, propType))
		
	def saveSettings(self):
		configParser = ConfigParser.RawConfigParser()
		configParser.optionxform = str
		
		Config.save(configParser)
		
		# save systems settings
		for (key, system) in self.systems.items():
			configParser.add_section(key)
			items = getProperties(system)
			for prop in items:
				value = getattr(system, prop)
				configParser.set(key, prop, string(value))
				
		with open(Config.ConfigFileName, 'wb') as configfile:
			configParser.write(configfile)
