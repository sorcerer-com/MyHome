import logging
import time
from Config import *

class Logger:
	# TODO: may be rename last log file
	logger = logging.getLogger()
	logger.setLevel(logging.DEBUG)
	logger.addHandler(logging.FileHandler(Config.LogFileName, mode="w"))
	
	data = []

	@staticmethod
	def log(type, msg):
		formatted = Logger.format(type, msg)
		getattr(Logger.logger, type)(formatted)
		if type <> "debug":
			Logger.data.append(formatted)
		if Config.PrintLog and type <> "debug":
			print formatted
		
	@staticmethod
	def format(type, msg):
		return time.strftime("%d/%m/%Y %H:%M:%S") + "\t" + type + "\t" + msg