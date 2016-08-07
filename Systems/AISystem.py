import warnings, threading
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
		
	def loadSettings(self, configParser, data):
		BaseSystem.loadSettings(self, configParser, data)

	def saveSettings(self, configParser, data):
		BaseSystem.saveSettings(self, configParser, data)

	def _onEnabledChanged(self):
		BaseSystem._onEnabledChanged(self)

	def update(self):
		BaseSystem.update(self)
		
		
	def onEventReceived(self, sender, event, data):
		if self._enabled:
			print "%s %s %s" % (sender.Name, event, data)
	
	
	@staticmethod
	def _say(text):
		text = "beep %s beep" % text
		with warnings.catch_warnings():
			warnings.simplefilter("ignore")
			
			tts = gTTS(text=text, lang="cs", debug=False) # pl/cs
			tts.save("say.mp3")
		
		PCControlService.openMedia(os.path.join(os.getcwd(), "say.mp3"), "local", 100)
		
		os.remove("say.mp3")
	
	@staticmethod
	def say(text):
		t = threading.Thread(target=lambda: AISystem._say(text))
		t.daemon = True
		t.start()