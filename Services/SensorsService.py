import subprocess
from Utils.Logger import *

class SensorsService:
	MotionSensorPin = -1

	@staticmethod
	def motionDetected():
		Logger.log("info", "motion detected")
		# TODO: implement
		return True