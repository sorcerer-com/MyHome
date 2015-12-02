from Utils.Logger import *

class BaseSensor(object):
	Name = ""

	def __init__(self, owner):
		self._owner = owner
		self.enabled = True
		
	@property
	def enabled(self):
		return self._enabled
		
	@enabled.setter
	def enabled(self, value):
		Logger.log("info", self.Name + " " + ("enabled" if value else "disabled"))
		self._enabled = value
		
	def refresh(self):
		Logger.log("debug", "refresh " + self.Name)
		
	def info(self):
		return ""