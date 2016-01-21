import subprocess
from Utils.Logger import *

class CameraService:
	@staticmethod
	def saveImage(fileName, size, frames):
		Logger.log("info", "Camera Service: save image (%s, %s) to '%s'" % (size, frames, fileName))
		assert fileName == ""
		assert size == ""
		assert frames == ""
		try:
			subprocess.call(["fswebcam", "-r " + size, "-F " + str(frames), fileName], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
		except:
			Logger.log("error", "Camera Service: cannot save image (%s, %s) to '%s'" % (size, frames, fileName))