if True:
	import RPi.GPIO as GPIO
	from Utils.Logger import *

	class SensorsService:
		MotionSensorPin = 7
		
		GPIO.setmode(GPIO.BCM)
		# Motion Sensor
		GPIO.setup(MotionSensorPin, GPIO.IN)
		GPIO.add_event_detect(MotionSensorPin, GPIO.RISING)


		@staticmethod
		def detectMotion():
			try:
				if GPIO.event_detected(SensorsService.MotionSensorPin):
					Logger.log("info", "Sensors Service: motion detected")
					return True
				return False
			except Exception as e:
				Logger.log("error", "Sensors Service: cannot detect motion")
				Logger.log("debug", str(e))
				return False

else:
	class SensorsService:
		@staticmethod
		def detectMotion():
			return True