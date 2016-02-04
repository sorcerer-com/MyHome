import os
from datetime import *
from BaseSystem import *
from Services.PCControlService import *

class MediaPlayerSystem(BaseSystem):
	Name = "MediaPlayer"

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self.rootPath = "~/Public"
		
	@property
	def list(self):
		if not os.path.isdir(self.rootPath):
			return []
			
		result = []
		for root, subFolders, files in os.walk(self.rootPath):
			for f in files:
				path = os.path.join(root, f)
				path = os.path.relpath(path, self.rootPath)
				result.append(str(len(result)) + ") " + path)
		return result
		
	@list.setter
	def list(self, value):
		pass
		
	@property
	def select(self):
		return 0
	
	@select.setter
	def select(self, value):
		if value >= len(self.list) or value < 0:
			return
			
		path = self.list[value]
		path = path[path.find(" ") + 1:] # remove index
		path = os.path.join(self.rootPath, path)
		PCControlService.openMedia(path)
		