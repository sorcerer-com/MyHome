from Utils.Logger import *

class BaseSystem(object):
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
		
	def update(self):
		Logger.log("debug", "update " + self.Name)