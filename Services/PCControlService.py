import subprocess
from Utils.Logger import *
from External.pymouse import PyMouse
from External.pykeyboard import PyKeyboard

class PCControlService:
	Mouse = PyMouse()
	Keyboard = PyKeyboard()
	
	# TODO: may be implement them as platform independent
	@staticmethod
	def openBrowser(url, wait=True):
		Logger.log("info", "PCControl Service: open browser with url '%s'" % url)
		try:
			call = subprocess.call if wait else subprocess.Popen
			return call(["sensible-browser", url], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
		except Exception as e:
			Logger.log("error", "PCControl Service: cannot open browser with url '%s'" % url)
			Logger.log("debug", str(e))
			return None
		
	@staticmethod
	def openMedia(path, wait=True):
		Logger.log("info", "PCControl Service: open media '%s'" % path)
		try:
			call = subprocess.call if wait else subprocess.Popen
			return call(["omxplayer", "-r", path], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
		except Exception as e:
			Logger.log("error", "PCControl Service: cannot open media '%s'" % path)
			Logger.log("debug", str(e))
			return None