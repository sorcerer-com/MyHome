from Utils.Logger import *

class BaseSensor:
	Name = ""

	def __init__(self, owner):
		self.Owner = owner
		self.Enabled = True
		
	def refresh(self):
		Logger.log("debug", "refresh " + self.Name)
		
	def info(self):
		return ""