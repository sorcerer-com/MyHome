from configparser import RawConfigParser
from os import path

from Utils import Utils
from Utils.Decorators import type_check
from Utils.Singleton import Singleton


class Config(Singleton):
	LogFilePath = "bin/log.log"
	ConfigFilePath = "bin/config.ini"

	@type_check
	def __init__(self):
		""" Initialize singleton Config instace. """

		self.load()

	@type_check
	def load(self) -> bool:
		""" Loads configurations from the config file.
		
		Returns:
			bool -- True if the loading is successful, otherwise False.
		"""

		configParser = RawConfigParser()
		configParser.optionxform = str
		if Config.ConfigFilePath not in configParser.read(Config.ConfigFilePath):
			return False

		items = configParser.items(self.__class__.__name__)
		for (name, value) in items:
			if hasattr(self, name):
				value = Utils.parse(value, type(getattr(self, name)))
				setattr(self, name, value)
		return True

	@type_check
	def save(self, configParser:RawConfigParser) -> None:
		""" Saves configurations to the config file.
		
		Arguments:
			configParser {RawConfigParser} -- ConfigParser which will be used to save configurations.
		"""

		section = self.__class__.__name__
		configParser.add_section(section)
		items = Utils.getFields(self)
		for attr in items:
			value = getattr(self, attr)
			configParser.set(section, attr, Utils.string(value))
