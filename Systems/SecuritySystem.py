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
		
		self.startDelay = timedelta(minutes=5)
		self.sendInterval = timedelta(minutes=5)
		self.numImages = 30
		
		self.activated = False
		self.currImage = 0
		self.lastSendTime = datetime.now()

	def _onEnabledChanged(self):
		BaseSystem._onEnabledChanged(self)
		self.lastSendTime = datetime.now() - self.sendInterval + self.startDelay

	def update(self):
		BaseSystem.update(self)
		
		# TODO: add options in UI		
		elapsed = datetime.now() - self.lastSendTime
		if elapsed > self.sendInterval:
			if self.activated:
				images = []
				for i in range(0, self.currImage):
					images.append("camera" + str(i) + ".jpg")
				InternetService.sendEMail([Config.EMail], "My Home", "Security Alarm Activated!", images)
				#InternetService.sendSMS(Config.GSMNumber, "Security Alarm Activated!", "telenor") # TODO: 
				for i in range(0, self.currImage):
					os.remove("camera" + str(i) + ".jpg")
			
			self.activated = False
		
		if not self.activated: # if not activated check for motion
			self.activated = SensorsService.motionDetected()
			if self.activated:
				Logger.log("info", "Security System: Activated")
			self.currImage = 0
			self.lastSendTime = datetime.now()
		
		if self.activated and elapsed > self.sendInterval / self.numImages * self.currImage:
			CameraService.saveImage("camera" + str(self.currImage) + ".jpg", "640x480", 1)
			self.currImage += 1
