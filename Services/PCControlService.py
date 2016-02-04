import subprocess
from Utils.Logger import *
from External.pymouse import PyMouse
from External.pykeyboard import PyKeyboard

class PCControlService:
	Mouse = PyMouse()
	Keyboard = PyKeyboard()
	
	# TODO: may be implement them as platform independent
	@staticmethod
	def openBrowser(url):
		Logger.log("info", "PCControl Service: open browser with url '%s'" % url)
		try:
			subprocess.call(["sensible-browser", url], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
		except Exception as e:
			Logger.log("error", "PCControl Service: cannot open browser with url '%s'" % url)
			Logger.log("debug", str(e))
		
	@staticmethod
	def openMedia(path):
		Logger.log("info", "PCControl Service: open media '%s'" % path)
		try:
			subprocess.call(["omxplayer", "-r", path], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
		except Exception as e:
			Logger.log("error", "PCControl Service: cannot open media '%s'" % path)
			Logger.log("debug", str(e))