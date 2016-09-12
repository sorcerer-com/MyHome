import os
from datetime import *
from BaseSystem import *
from Services.PCControlService import *

class MediaPlayerSystem(BaseSystem):
	Name = "MediaPlayer"
	_Formats = [".mkv", ".avi", ".mov", ".wmv", ".mp4", ".mpg", ".mpeg", ".m4v", ".3gp"]

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self.rootPath = "~/Public"
		self.volume = 0
		
		self._playing = ""
		self._process = None 
		
	@property
	def _list(self):
		if not os.path.isdir(self.rootPath):
			return []
			
		result = []
		for root, subFolders, files in os.walk(self.rootPath):
			subFolders.sort()
			files.sort()
			for f in files:
				if os.path.splitext(f)[1] in self._Formats:
					path = os.path.join(root, f)
					result.append(os.path.relpath(path, self.rootPath))
		return result
		
	def getPlaying(self):
		if (self._process is None) or (self._process.poll() is not None):
			self._playing = ""
			self._process = None
		return self._playing
		
		
	def play(self, path):
		# mark as watched
		dirPath = os.path.dirname(os.path.join(self.rootPath, path))
		fileName = os.path.splitext(os.path.basename(path))[0] # file name without extension
		for file in os.listdir(dirPath):
			if file.startswith(fileName) and not os.path.splitext(file)[0].endswith("_w"):
				os.rename(os.path.join(dirPath, file), os.path.join(dirPath, fileName + "_w" + os.path.splitext(file)[1]))
		if not os.path.splitext(path)[0].endswith("_w"):
			path = os.path.splitext(path)[0] + "_w" + os.path.splitext(path)[1]
		# play
		self._playing = path
		path = os.path.join(self.rootPath, path)
		self._process = PCControlService.openMedia(path, wait=False)
		if (self._process is not None) and (self._process.poll() is None):
			# adjust volume
			time.sleep(2)
			for i in range(0, self.volume):
				self._process.stdin.write("+")
				time.sleep(0.1)
			for i in range(self.volume, 0):
				self._process.stdin.write("-")
				time.sleep(0.1)
			self._owner.event(self, "MediaPlayed", self._playing)
		
	def stop(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("q")
			self._playing = ""
			self._owner.event(self, "MediaStoped")
		
	def pause(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write(" ") # space
			self._owner.event(self, "MediaPaused")
		
	def volumeDown(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("-")
			self.volume -= 1
			self._owner.systemChanged = True
			self._owner.event(self, "MediaVolumeDown")
		
	def volumeUp(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("+")
			self.volume += 1
			self._owner.systemChanged = True
			self._owner.event(self, "MediaVolumeUp")
		
	def seekBack(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("\027[D") # left arrow
			self._owner.event(self, "MediaSeekBack")
		
	def seekForward(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("\027[C") # right arrow
			self._owner.event(self, "MediaSeekForward")
		
	def seekBackFast(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("\027[B") # down arrow
			self._owner.event(self, "MediaSeekBackFast")
		
	def seekForwardFast(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("\027[A") # up arrow
			self._owner.event(self, "MediaSeekForwardFast")