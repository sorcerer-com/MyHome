from BaseSensor import *

class DistanceSensor(BaseSensor):
	Name = "Distance"

	def __init__(self, owner):
		BaseSensor.__init__(self, owner)
		
		self.Distance = 0.0
		self.Changes = 10.0 # TODO: 0.0
	
	def refresh(self):
		BaseSensor.refresh(self)
		
	def info(self):
		return str(self.Distance) + " (" + str(self.Changes) + ")"