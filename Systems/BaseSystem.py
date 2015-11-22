from Utils.Logger import *

class BaseSystem:
	Name = ""

	def __init__(self, owner):
		self.Owner = owner
		self.Enabled = True
		
	def update(self):
		Logger.log("debug", "update " + self.Name)