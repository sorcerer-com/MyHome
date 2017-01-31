import os, time
from datetime import *
from BaseSystem import *
from Systems.SensorsSystem import *
from Services.PCControlService import *
from Services.InternetService import *

class SecuritySystem(BaseSystem):
	Name = "Security"

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self.startDelay = timedelta(minutes=15)
		self.sendInterval = timedelta(minutes=5)
		self.numImages = 30
		
		self._activated = False
		self._imageCount = 0
		self._lastSendTime = datetime.now()
		self._prevImg = None

	def _onEnabledChanged(self):
		BaseSystem._onEnabledChanged(self)
		self._activated = False
		self._lastSendTime = datetime.now() + self.startDelay
		self.clearImages()
		self._prevImg = None

	def update(self):
		BaseSystem.update(self)
		
		elapsed = datetime.now() - self._lastSendTime
		if elapsed > self.sendInterval:
			if self._activated or self._imageCount != 0:
				images = []
				for i in range(0, self._imageCount):
					images.append("image%02d.jpg" % i)
				try:
					msg = "Security Alarm Activated!\n%s" % self._owner.systems[SensorsSystem.Name].getLatestData()
					if not InternetService.sendSMS(Config.GSMNumber, "telenor", msg):
						raise Exception()
					if InternetService.sendEMail([Config.EMail], "My Home", msg, images): # if send successful
						self.clearImages()
						self._activated = False
					else:
						raise Exception()
				except Exception as e:
					Logger.log("warning", "Security System: cannot send email or sms")
					Logger.log("debug", str(e))
					
		if not self._activated and elapsed > timedelta(): # if not _activated and after delay start - check for motion
			self._activated = self._owner.systems[SensorsSystem.Name].detectAnyMotion()
			if self._activated:
				Logger.log("info", "Security System: Alarm Activated")
			self._lastSendTime = datetime.now()
		elif not self._activated:
			self._owner.systems[SensorsSystem.Name].detectAnyMotion()
		
		if self._activated:
			img = self._owner.systems[SensorsSystem.Name].getImage()
			if img == None:
				PCControlService.captureImage("image%02d.jpg" % self._imageCount, "640x480", 1, 4)				
			if elapsed > (self.sendInterval / self.numImages) * (self._imageCount % (self.numImages + 1)) or self.findMotion(self._prevImg, img):
				self._prevImg = img
				if img != None:
					img.save("image%02d.jpg" % self._imageCount)
					Logger.log("info", "Security System: capture image to 'image%02d.jpg'" % self._imageCount)
				self._imageCount += 1

	def clearImages(self):
		for i in range(0, self._imageCount):
			if os.path.isfile("image%02d.jpg" % i):
				os.remove("image%02d.jpg" % i)
		self._imageCount = 0
	
	
	@staticmethod
	def findMotion(prevImg, img):
		if prevImg == None or img == None:
			return False
			
		diff = img - prevImg
		diff = diff.binarize(32).invert()
		matrix = diff.getNumpy()
		mean = matrix.mean()
		return mean > 0.1