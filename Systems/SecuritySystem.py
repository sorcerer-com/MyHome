import os
from datetime import *
from BaseSystem import *
from Services.SensorsService import *
from Services.CameraService import *
from Services.InternetService import *

class SecuritySystem(BaseSystem):
	Name = "Security"

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		self._enabled = False
		
		self.startDelay = timedelta(minutes=15)
		self.sendInterval = timedelta(minutes=5)
		self.numImages = 30
		
		self._activated = False
		self._currImage = 0
		self._lastSendTime = datetime.now()

	def _onEnabledChanged(self):
		BaseSystem._onEnabledChanged(self)
		self._activated = False
		self.clearImages()
		self._lastSendTime = datetime.now() + self.startDelay

	def update(self):
		BaseSystem.update(self)
		
		elapsed = datetime.now() - self._lastSendTime
		if elapsed > self.sendInterval:
			if self._activated:
				InternetService.sendSMS(Config.GSMNumber, "Security Alarm Activated!", "telenor")
				images = []
				for i in range(0, self._currImage):
					images.append("camera" + str(i) + ".jpg")
				if InternetService.sendEMail([Config.EMail], "My Home", "Security Alarm Activated!", images): # if send successful
					self.clearImages()
			
			self._activated = False
		
		if not self._activated and elapsed > timedelta(): # if not _activated and after delay start - check for motion
			self._activated = SensorsService.motionDetected()
			if self._activated:
				Logger.log("info", "Security System: Activated")
			self._lastSendTime = datetime.now()
		elif not self._activated:
			SensorsService.motionDetected()
		
		if self._activated and elapsed > (self.sendInterval / self.numImages) * (self._currImage % (self.numImages + 1)):
			CameraService.saveImage("camera" + str(self._currImage) + ".jpg", "640x480", 1, 2)
			self._currImage += 1

	def clearImages(self):
		for i in range(0, self._currImage):
			if os.path.isfile("camera" + str(i) + ".jpg"):
				os.remove("camera" + str(i) + ".jpg")
		self._currImage = 0