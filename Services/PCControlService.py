import subprocess
from Utils.Logger import *
from External.pymouse import PyMouse
from External.pykeyboard import PyKeyboard

class PCControlService:
	@staticmethod
	def mouse():
		return PyMouse()
		
	@staticmethod
	def keyboard():
		return PyKeyboard()
	
	# TODO: may be implement them as platform independent
	@staticmethod
	def captureImage(fileName, size, frames, skipFrames, wait=True):
		Logger.log("info", "PCControl Service: capture image (%s, %s) to '%s'" % (size, frames, fileName))
		if (fileName == "") or (size == "") or (frames == ""):
			Logger.log("error", "PCControl Service: cannot capture image - invalid parameters")
			return None

		try:
			call = subprocess.call if wait else subprocess.Popen
			return call(["fswebcam", "-r", str(size), "-F", str(frames), "-S", str(skipFrames), fileName], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
		except Exception as e:
			Logger.log("error", "PCControl Service: cannot capture image (%s, %s) to '%s'" % (size, frames, fileName))
			Logger.log("debug", str(e))
			return None
			
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
			return call(["omxplayer", "-r", "-o", "hdmi", path], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
		except Exception as e:
			Logger.log("error", "PCControl Service: cannot open media '%s'" % path)
			Logger.log("debug", str(e))
			return None