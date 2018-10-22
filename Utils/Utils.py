import logging
import logging.handlers


def setupLogging(fileName:str, showInConsole:bool = True, useBufferHandler:bool = True):
	"""Setup Logging module.
	
	:param fileName: Path to the file which will contains the logs.
	:type fileName: str
	:param showInConsole: If True the logs will be shown in the standart output, defaults to True
	:param showInConsole: bool, optional
	:param useBufferHandler: If True the logs will be collected in the buffer, defaults to True
	:param useBufferHandler: bool, optional
	"""

	logger = logging.getLogger()
	logger.setLevel(logging.DEBUG)
	formatter = logging.Formatter("%(asctime)-20s %(name)-12s %(levelname)-8s %(message)s", "%d/%m/%Y %H:%M:%S")

	# add RotatingFileHandler
	file = logging.handlers.RotatingFileHandler(fileName, maxBytes=1024*1024, backupCount=3)
	file.setFormatter(formatter)
	logger.addHandler(file)

	if showInConsole:
		# add Console handler
		console = logging.StreamHandler()
		console.setLevel(logging.INFO)
		console.setFormatter(formatter)
		logger.addHandler(console)

	if useBufferHandler:
		# add BufferHandler
		buffer = logging.handlers.BufferingHandler(500)
		buffer.setFormatter(formatter)
		logger.addHandler(buffer)

def getLogs():
	"""Return list of the last 500 log records.
	
	:return: List of the last 500 log records.
	:rtype: List of strings.
	"""

	logger = logging.getLogger()
	handlers = list(h for h in logger.handlers if isinstance(h, logging.handlers.BufferingHandler)) # get all BufferingHandlers
	if len(handlers) > 0:
		return [handlers[0].formatter.format(rec) for rec in handlers[0].buffer]
	return None	