#!/usr/bin/python
from threading import Timer
from Utils.Logger import *
from Systems.SecurityAlarmSystem import *
from Sensors.DistanceSensor import *

# TODO: may be implement one day(month) tests - check if everything is ok
class MHome():
	UpdateTime = 0.01

	def __init__(self):
		Logger.log("info", "Start My Home")
		
		# systems
		self.Systems = {}
		self.Systems[SecurityAlarmSystem.Name] = SecurityAlarmSystem(self)
		
		# sensors
		self.Sensors = {}
		self.Sensors[DistanceSensor.Name] = DistanceSensor(self)
		
		self.update()
		
	def __del__(self):
		MHome.UpdateTime = 0

	def update(self):
		# refresh sensors data
		for sensor in self.Sensors.values():
			if sensor.Enabled:
				sensor.refresh()
				
		# update systems
		for system in self.Systems.values():
			if system.Enabled:
				system.update()
				
		if MHome.UpdateTime > 0:
			Timer(MHome.UpdateTime, self.update).start()