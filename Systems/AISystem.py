import warnings
from gtts import gTTS
from BaseSystem import *
from Services.PCControlService import *
from Utils.Utils import *

class AISystem(BaseSystem):
	Name = "AI"

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self._owner.event += lambda s, e, d=None: self.onEventReceived(s, e, d)
		
		self._enabled = False
		
	def loadSettings(self, configParser):
		BaseSystem.loadSettings(self, configParser)

	def saveSettings(self, configParser):
		BaseSystem.saveSettings(self, configParser)

	def _onEnabledChanged(self):
		BaseSystem._onEnabledChanged(self)

	def update(self):
		BaseSystem.update(self)
		
		
	def onEventReceived(self, sender, event, data):
		if self._enabled:
			print "%s %s %s" % (sender.Name, event, data)
	
	
	@staticmethod
	def say(text):
		with warnings.catch_warnings():
			warnings.simplefilter("ignore")
			
			tts = gTTS(text=text, lang="cs", debug=False) # pl/cs
			tts.save("1.mp3")
		
		PCControlService.openMedia(os.path.join(os.getcwd(), "1.mp3"), "local")
		
		os.remove("1.mp3")
