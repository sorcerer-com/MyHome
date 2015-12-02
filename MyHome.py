#!/usr/bin/python
from threading import Timer
from Utils.Logger import *
from Systems.SecurityAlarmSystem import *
from Sensors.DistanceSensor import *

# TODO: may be implement one day(month) tests - check if everything is ok
class MHome():
	updateTime = 0.01

	def __init__(self):
		Logger.log("info", "Start My Home")
		
		# systems
		self.systems = {}
		self.systems[SecurityAlarmSystem.Name] = SecurityAlarmSystem(self)
		
		# sensors
		self.sensors = {}
		self.sensors[DistanceSensor.Name] = DistanceSensor(self)
		
		self.update()
		
	def __del__(self):
		MHome.updateTime = 0

	def update(self):
		# refresh sensors data
		for sensor in self.sensors.values():
			if sensor.enabled:
				sensor.refresh()
				
		# update systems
		for system in self.systems.values():
			if system.enabled:
				system.update()
				
		if MHome.updateTime > 0:
			Timer(MHome.updateTime, self.update).start()