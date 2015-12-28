from Utils.Logger import *

class BaseSystem(object):
	Name = ""

	def __init__(self, owner):
		self._owner = owner
		self._enabled = True
		
	@property
	def enabled(self):
		return self._enabled
		
	@enabled.setter
	def enabled(self, value):
		if self._enabled <> value:
			self._enabled = value
			self._onEnabledChanged()
		
	def _onEnabledChanged(self):
		Logger.log("info", self.Name + "System " + ("enabled" if self.enabled else "disabled"))
		
	def update(self):
		Logger.log("debug", "update " + self.Name + " System")