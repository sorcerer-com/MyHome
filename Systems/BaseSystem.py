from Utils.Logger import *
from Utils.Utils import *

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
			self._owner.systemChanged = True
			self._onEnabledChanged()
		
	def _onEnabledChanged(self):
		Logger.log("info", self.Name + " System " + ("enabled" if self.enabled else "disabled"))
		self._owner.event(self, "EnabledChanged", self.enabled)
		
	def update(self):
		pass
		
	def loadSettings(self, configParser, data):
		if not configParser.has_section(self.Name):
			return
			
		items = configParser.items(self.Name)
		for (name, value) in items:
			if hasattr(type(self), name):
				propType = type(getattr(type(self), name))
				if propType is not property:
					setattr(type(self), name, parse(value, propType))
			if hasattr(self, name):
				propType = type(getattr(self, name))
				setattr(self, name, parse(value, propType))

	def saveSettings(self, configParser, data):
		items = getProperties(self, True)
		for prop in items:
			value = getattr(self, prop)
			configParser.set(self.Name, prop, string(value))
		