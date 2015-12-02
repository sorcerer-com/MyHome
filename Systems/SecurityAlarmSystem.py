import os
from datetime import *
from BaseSystem import *
from Sensors.DistanceSensor import *
from Services.CameraService import *
from Services.InternetService import *

class SecurityAlarmSystem(BaseSystem):
	Name = "SecurityAlarm"

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self.enabled = False
		self.lastActivateTime = datetime.now()
		
	def update(self):
		BaseSystem.update(self)
		
		# TODO: may be take images often (with other timer) and add all of them in the mail
		# TODO: may be add delay enabling
		elapsed = datetime.now() - self.lastActivateTime
		if self._owner.sensors[DistanceSensor.Name].changes > 1.0 and (elapsed > timedelta(minutes=0,seconds=2)): # TODO: 5 minutes
			Logger.log("info", "SecurityAlarm Activated!!!")
			CameraService.saveImage("temp.jpg", "640x480", 10)
			#InternetService.sendEMail([Config.EMail], "My Home", "Security Alarm Activated!", ["temp.jpg"])
			#InternetService.sendSMS(Config.GSMNumber, "Security Alarm Activated!", "telenor")
			os.remove("temp.jpg")
			self.lastActivateTime = datetime.now()