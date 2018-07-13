import logging, time, os

from Config import Config

class Logger:		
	logger = logging.getLogger()
	logger.setLevel(logging.DEBUG)
	logger.addHandler(logging.FileHandler(Config.LogFileName))
	
	data = []

	@staticmethod
	def log(type, msg):
		try:
			formatted = Logger.format(type, msg)
			getattr(Logger.logger, type)(formatted)
			if type <> "debug" and type <> "exception":
				Logger.data.append(formatted)
				if len(Logger.data) > 700:
					Logger.data = Logger.data[len(Logger.data) - 500:]
			if Config.PrintLog and type <> "debug" and type <> "exception":
				print formatted
			Logger.backup()
		except Exception as e:
			print str(e)

	@staticmethod
	def format(type, msg):
		return "%s    %-10s %s" % (time.strftime("%d/%m/%Y %H:%M:%S"), type, msg)
		
	@staticmethod
	def backup():
		# if log file exists and its size is greater than set maximum backup it
		if os.path.isfile(Config.LogFileName) and os.stat(Config.LogFileName).st_size > Config.LogMaxSize:
			Logger.logger.removeHandler(Logger.logger.handlers[0])
			if os.path.isfile(Config.LogFileName + ".bak"):
				os.remove(Config.LogFileName + ".bak")
			os.rename(Config.LogFileName, Config.LogFileName + ".bak")
			Logger.logger.addHandler(logging.FileHandler(Config.LogFileName))