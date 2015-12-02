from BaseSensor import *

class DistanceSensor(BaseSensor):
	Name = "Distance"

	def __init__(self, owner):
		BaseSensor.__init__(self, owner)
		
		self.distance = 0.0
		self.changes = 10.0 # TODO: 0.0
	
	def refresh(self):
		BaseSensor.refresh(self)
		
	def info(self):
		return str(self.distance) + " (" + str(self.changes) + ")"