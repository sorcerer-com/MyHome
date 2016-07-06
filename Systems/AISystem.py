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
	
	
	_LastSayTime = datetime.now() - timedelta(hours=2)
	
	@staticmethod
	def say(text):
		if AISystem._LastSayTime < datetime.now() - timedelta(hours=2):
			AISystem._LastSayTime = datetime.now()
			AISystem.say("beeeeep")
			
		with warnings.catch_warnings():
			warnings.simplefilter("ignore")
			
			tts = gTTS(text=text, lang="cs", debug=False) # pl/cs
			tts.save("say.mp3")
		
		PCControlService.openMedia(os.path.join(os.getcwd(), "say.mp3"), "local")
		
		os.remove("say.mp3")
