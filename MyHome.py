#!/usr/bin/python
from threading import Timer
from Utils.Logger import *
from Systems.SecuritySystem import *

# TODO: may be implement one day(month) tests - check if everything is ok
class MHome():
	updateTime = 0.01

	def __init__(self):
		Logger.log("info", "Start My Home")
		Config.load()
		
		# systems
		self.systems = {}
		self.systems[SecuritySystem.Name] = SecuritySystem(self)
		
		self.update()
		
	def __del__(self):
		MHome.updateTime = 0
		Config.save();

	def update(self):				
		# update systems
		for system in self.systems.values():
			if system.enabled:
				system.update()
				
		if MHome.updateTime > 0:
			Timer(MHome.updateTime, self.update).start()