import subprocess
from Utils.Logger import *

class CameraService:
	@staticmethod
	def saveImage(fileName, size, frames):
		Logger.log("info", "call " + str(["fswebcam", "-r " + size, "-F " + str(frames), fileName]))
		subprocess.call(["fswebcam", "-r " + size, "-F " + str(frames), fileName], stdout=subprocess.PIPE, stderr=subprocess.PIPE)