import subprocess
from Utils.Logger import *

class CameraService:
	@staticmethod
	def saveImage(fileName, size, frames):
		Logger.log("info", "Camera Service: save image (%s, %s) to '%s'" % (size, frames, fileName))
		if (fileName == "") or (size == "") or (frames == ""):
			Logger.log("error", "CameraService: cannot save image - invalid parameters")
			return

		try:
			subprocess.call(["fswebcam", "-r " + size, "-F " + str(frames), fileName], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
		except Exception as e:
			Logger.log("error", "Camera Service: cannot save image (%s, %s) to '%s'" % (size, frames, fileName))
			Logger.log("debug", str(e))