import os
import socket

from External.pysmb.nmb.NetBIOS import NetBIOS
from External.pysmb.smb.SMBConnection import SMBConnection

from Utils.Logger import Logger
from BaseSystem import BaseSystem
from Services.PCControlService import PCControlService

class MediaPlayerSystem(BaseSystem):
	Name = "MediaPlayer"
	_Formats = [".mkv", ".avi", ".mov", ".wmv", ".mp4", ".mpg", ".mpeg", ".m4v", ".3gp", ".mp3"]

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		self._enabled = None
		
		self.rootPath = "~/Public"
		self.sharedPath = ""
		self.volume = 0
		self.radios = []
		self.radios.append("http://193.108.24.21:8000/fresh")
		self.radios.append("http://149.13.0.80/nrj.ogg")
		self.radios.append("http://46.10.150.123:80/njoy.mp3")
		self.radios.append("http://149.13.0.81/bgradio.ogg")
		self.radios.append("http://149.13.0.81/radio1rock.ogg")
		
		self._sharedList = []
		self._playing = ""
		self._process = None 
		
	@property
	def _list(self):
		result = []
		if os.path.isdir(self.rootPath):
			for root, subFolders, files in os.walk(self.rootPath):
				subFolders.sort()
				files.sort()
				for f in files:
					if os.path.splitext(f)[1] in self._Formats:
						path = os.path.join(root, f)
						result.append(os.path.relpath(path, self.rootPath))
		# shared
		if len(self._sharedList) > 0:
			result.append("----- Shared -----")
			result.extend(self._sharedList)
		# add radios
		if len(self.radios) > 0:
			result.append("----- Radios -----")
			result.extend(self.radios)
		return result
		
	def getPlaying(self):
		if (self._process is None) or (self._process.poll() is not None):
			self._playing = ""
			self._process = None
		return self._playing
		
		
	def play(self, path):
		if path.startswith("-"):
			return
		if path in self.radios:
			self._playing = path
			self._process = PCControlService.openMedia(path, "local", int(self.volume * 300 * 1.5), False)
		else:
			if not path in self._sharedList:
				path = self._markAsWatched(path)
				self._playing = path
				path = os.path.join(self.rootPath, path)
			else:
				self._playing = path
				path = self.sharedPath + path
			self._process = PCControlService.openMedia(path, volume=self.volume*300, wait=False)
		if (self._process is not None) and (self._process.poll() is None):
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
			
	def command(self, cmd):
		if (self._process is not None) and (self._process.poll() is None):
			self._process.stdin.write(cmd)
			self._owner.event(self, "MediaCommand", cmd)
	
	def refreshSharedList(self):
		self._sharedList = self._listShared()
		
	
	def _markAsWatched(self, path):
		try:
			dirPath = os.path.dirname(os.path.join(self.rootPath, path))
			fileName = os.path.splitext(os.path.basename(path))[0] # file name without extension
			for file in os.listdir(dirPath):
				if file.startswith(fileName) and not os.path.splitext(file)[0].endswith("_w"):
					os.rename(os.path.join(dirPath, file), os.path.join(dirPath, fileName + "_w" + os.path.splitext(file)[1]))
					
			if not os.path.splitext(path)[0].endswith("_w"):
				path = os.path.splitext(path)[0] + "_w" + os.path.splitext(path)[1]
		except Exception as e:
			Logger.log("error", "MediaPlayer System: cannot mark file as watched: " + path)
			Logger.log("exception", str(e))
		return path
		
	def _listShared(self):
		result = []
		try:
			path = self.sharedPath
			# path format should be: smb://username:password@host_ip/folder/
			if not path.startswith("smb://"):
				return result
			path = path[6:]

			if not ":" in path or not "@" in path:
				return result

			username = path.split(":")[0]
			path = path[len(username)+1:]

			password = path.split("@")[0]
			path = path[len(password)+1:]

			server_ip = path.split("/")[0]
			path = path[len(server_ip)+1:]

			basepath = path.split("/")[0]
			path = path[len(basepath):]
			
			hostname = socket.gethostname()
			if hostname:
				hostname = hostname.split('.')[0]
			else:
				hostname = 'SMB%d' % os.getpid()

			netBios = NetBIOS()
			server_name = netBios.queryIPForName(server_ip)[0]
			netBios.close()
			
			conn = SMBConnection(username, password, hostname, server_name, use_ntlm_v2 = True)
			assert conn.connect(server_ip)

			subPaths = [path] # recursion list
			while len(subPaths) > 0:
				for file in  conn.listPath(basepath, subPaths[0]):
					if file.isDirectory and not file.filename.startswith("."):
						subPaths.append(os.path.join(subPaths[0], file.filename))
					elif os.path.splitext(file.filename)[1] in self._Formats:
						fullpath = self.sharedPath.strip("/") + os.path.join(subPaths[0], file.filename)
						result.append(os.path.relpath(fullpath, self.sharedPath))
				subPaths.remove(subPaths[0])
			conn.close()
		except Exception as e:
			Logger.log("error", "MediaPlayer System: cannot list shared folder")
			Logger.log("exception", str(e))
		return result