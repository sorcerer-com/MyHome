import os
from datetime import *
from BaseSystem import *
from Services.PCControlService import *

class MediaPlayerSystem(BaseSystem):
	Name = "MediaPlayer"

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self.rootPath = "~/Public"
		
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
				path = os.path.join(root, f)
				result.append(os.path.relpath(path, self.rootPath))
		return result
		
	def getPlaying(self):
		if (self._process is None) or (self._process.poll() is not None):
			self._playing = ""
			self._process = None
		return self._playing
		
		
	def play(self, path):
		self._playing = path
		path = os.path.join(self.rootPath, path)
		self._process = PCControlService.openMedia(path, False)
		
	def stop(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("\027") # escape
		
	def pause(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("\032") # space
		
	def volumeDown(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("-")
		
	def volumeUp(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("+")
		
	def seekBack(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("\027[D") # left arrow
		
	def seekForward(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("\027[C") # right arrow
		
	def seekBackFast(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("\027[B") # down arrow
		
	def seekForwardFast(self):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write("\027[A") # up arrow