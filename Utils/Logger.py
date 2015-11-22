import logging
import time
from Config import *

class Logger:
	# TODO: may be rename last log file
	logger = logging.getLogger()
	logger.setLevel(logging.INFO)
	logger.addHandler(logging.FileHandler(Config.ConfigFileName, mode="w"))

	@staticmethod
	def log(type, msg):
		getattr(logging, type)(Logger.format(type, msg))
		
	@staticmethod
	def format(type, msg):
		return time.strftime("%d/%m/%Y %H:%M:%S") + "\t" + type + "\t" + msg