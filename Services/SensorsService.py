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
		def motionDetected():
			if GPIO.event_detected(SensorsService.MotionSensorPin):
				Logger.log("info", "Sensors Service: motion detected")
				return True
			return False

else:
	class SensorsService:
		@staticmethod
		def motionDetected():
			return True