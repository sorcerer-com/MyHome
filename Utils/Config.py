import ConfigParser

class Config:
	# TODO: add them as options in UI
	ConfigFileName = "config.ini"
	LogFileName = "bin/log.txt"
	PrintLog = "True"
	
	GSMNumber = "359898555415"
	MyTelenorPassword = "f25163"
	SMTPServer = "smtp.abv.bg"
	SMTPServerPort = "465"
	EMail = "sorcerer_com@abv.bg"
	EMailUserName = "sorcerer_com@abv.bg"
	EMailPassword = "com123"
	
	@staticmethod
	def save():
		config = ConfigParser.RawConfigParser()
		config.optionxform = str
		config.add_section("Config")
		items = dir(Config)
		for attr in items:
			value = getattr(Config, attr)
			if (type(value) is str) and attr[0] <> '_':
				config.set("Config", attr, value)
		
		with open(Config.ConfigFileName, 'wb') as configfile:
			config.write(configfile)
			
	@staticmethod
	def load():
		config = ConfigParser.RawConfigParser()
		config.optionxform = str
		if Config.ConfigFileName not in config.read(Config.ConfigFileName):
			return
		items = config.items("Config")
		for (name, value) in items:
			if hasattr(Config, name):
				setattr(Config, name, value)
	