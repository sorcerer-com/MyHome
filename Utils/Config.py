import ConfigParser

class Config:
	ConfigFileName = "config.ini"
	LogFileName = "log.txt"
	PrintLog = "True"
	
	GSMNumber = "359898555415"
	MyTelenorPassword = "f25163"
	SMTPServer = "smtp.abv.bg"
	SMTPServerPort = "465"
	EMail = "sorcerer_com@abv.bg"
	EMailUserName = "sorcerer_com@abv.bg"
	EMailPassword = "com123"
	
	@staticmethod
	def list():
		result = []
		items = dir(Config)
		for attr in items:
			attrType = type(getattr(Config, attr))
			if (attrType is str) and (not attr.startswith("_")):
				result.append(attr)
		return result
	
	@staticmethod
	def save(configParser):
		configParser.add_section("Config")
		items = Config.list()
		for attr in items:
			value = getattr(Config, attr)
			configParser.set("Config", attr, value)
			
	@staticmethod
	def load(configParser):
		items = configParser.items("Config")
		for (name, value) in items:
			if hasattr(Config, name):
				setattr(Config, name, value)
	