import subprocess
from Utils.Logger import *

class CameraService:
	@staticmethod
	def saveImage(fileName, size, frames):
		Logger.log("info", "Camera Service: save image (%s, %s) to '%s'" % (size, frames, fileName))
		subprocess.call(["fswebcam", "-r " + size, "-F " + str(frames), fileName], stdout=subprocess.PIPE, stderr=subprocess.PIPE)