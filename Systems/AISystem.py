# -*- coding: utf-8 -*- 
import warnings, threading, random
from gtts import gTTS
from BaseSystem import *
from Services.PCControlService import *
from Services.InternetService import *
from Utils.Utils import *

class AISystem(BaseSystem):
	Name = "AI"
	VoiceVolume = -3

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self._owner.event += lambda s, e, d=None: self._onEventReceived(s, e, d)
		
		self._enabled = False
		
		self._commands = []
		self._voiceCommands = {}
		
		self._commands.append([u"result = u'%s и %s' % (datetime.now().hour % 12, datetime.now().minute)"])
		self._commands.append([u"weather = InternetService.getWeather()[0]", "{3}"])
		self._commands.append([u"weather = InternetService.getWeather()[1]", "{3}"])
		self._commands.append([u"result  = u'Времето ще е %s. ' % weather['condition'].replace(u', ', u' с ')",
							   u"result += u'Минималната температура ще е %s градуса, максималната %s. ' % (weather['minTemp'], weather['maxTemp'])",
							   u"result += u'Вятарът ще е %s от %s. ' % (weather['wind'].split(u', ')[1], weather['wind'].split(u', ')[0])",
							   u"if weather['rainProb'] > 30: result += u'Има %s процента вероятност за дъжд с интензитет %s мм. ' % (weather['rainProb'], weather['rainAmount'])",
							   u"if weather['stormProb'] > 30: result += u'Вероятността за буря е %s процента. ' % weather['stormProb']",
							   u"result += u'Oблачността ще е %s процента. ' % weather['cloudiness']"])
		
		self._voiceCommands[u"колко е часа"] = 0
		self._voiceCommands[u"какво ще е времето днес"] = 1
		self._voiceCommands[u"какво ще е времето утре"] = 2
		
	def loadSettings(self, configParser, data):
		BaseSystem.loadSettings(self, configParser, data)

	def saveSettings(self, configParser, data):
		BaseSystem.saveSettings(self, configParser, data)

	def _onEnabledChanged(self):
		BaseSystem._onEnabledChanged(self)

	def update(self):
		BaseSystem.update(self)
		
		
	def _onEventReceived(self, sender, event, data):
		if self._enabled:
			Logger.log("debug", "AI System: %s %s %s" % (sender.Name, event, data))
		
	def processVoiceCommand(self, transcript, confidence):
		Logger.log("debug", "AI System: " + transcript + " " + str(confidence))
		
		result = None
		# too low confidence
		if confidence < 0.5:
			temp = [u"Моля", u"Не ви чух", u"Може ли да повторите"]
			result = temp[random.randint(0, len(temp)-1)]
		# unknown command
		elif transcript not in self._voiceCommands.keys():
			temp = [u"Не ви разбирам", u"Не знам какво значи това", u"Какво значи това"]
			result = temp[random.randint(0, len(temp)-1)]
		# execute command
		else:
			try:
				command = self._getCommand(self._voiceCommands[transcript])
				exec(command)
				Logger.log("info", "AI System: execute command '%s'" % command.replace("\n", "\\n"))
				self._owner.event(self, "CommandExecuted", command)
			except Exception as e:
				Logger.log("error", u"Control System: cannot execute '%s'" % transcript)
				Logger.log("debug", str(e))
		
		if result != None:
			AISystem.say(result)
		return result
		
	def _getCommand(self, index):
		command = self._commands[index]
		i = 0
		while i < len(command):
			if command[i].startswith("{") and command[i].endswith("}"):
				idx = int(command[i][1:-1])
				command = command[:i] + self._commands[idx] + command[i+1:]
			else:
				i += 1
		return "\n".join(command)
	
	
	@staticmethod
	def _say(text):
		text = AISystem.symbolsToText(text)
		text = AISystem.digitToText(text)
		text = AISystem.cyrToLat(text)
		text = "bee eep %s bee eep" % text
		with warnings.catch_warnings():
			warnings.simplefilter("ignore")
			
			tts = gTTS(text=text, lang="cs", debug=False) # pl/cs
			tts.save("say.mp3")
		
		PCControlService.openMedia(os.path.join(os.getcwd(), "say.mp3"), "local", AISystem.VoiceVolume * 100)
		
		os.remove("say.mp3")
	
	@staticmethod
	def say(text):
		t = threading.Thread(target=lambda: AISystem._say(text))
		t.daemon = True
		t.start()
	
	@staticmethod
	def cyrToLat(text):
		symbols = (
			u"абвгдезийклмнопрстуфхАБВГДЕЗИЙКЛМНОПРСТУФХ",
			u"abwgdeziyklmnoprstufhABWGDEZIYKLMNOPRSTUFH")
		tr = {ord(a):ord(b) for a, b in zip(*symbols)}
		text = text.translate(tr)
		
		specialChars = {
			u"ж": u"zh",
			u"ц": u"c",
			u"ч": u"ch",
			u"ш": u"sh",
			u"щ": u"sht",
			u"ъ": u"а",
			u"ь": u"y",
			u"ю": u"yu",
			u"я": u"ya",
			u"Ж": u"Zh",
			u"Ц": u"Ts",
			u"Ч": u"Ch",
			u"Ш": u"Sh",
			u"Щ": u"Sht",
			u"Ъ": u"A",
			u"Ю": u"Yu",
			u"Я": u"Ya"}
		for (key, value) in specialChars.items():
			text = text.replace(key, value)
		return text
		
	@staticmethod
	def digitToText(text):
		ints = [int(s) for s in text.split() if s.isdigit()] # TODO: doubles
		for i in sorted(ints, reverse=True):
			s = u""
			if i < 0:
				s += u"минус "
			if i >= 100:
				continue
			if i >= 20:
				temp = [u"двайсет", u"трийсет", u"четиресет", u"педесет", u"шейсет", u"седемдесет", u"осемдесет", u"деведесет"]
				s += temp[int(i / 10) - 2]
				if (i % 10) != 0:
					s += u" и "
			if i >= 0 and (i == 10 or i % 10 != 0):
				temp = [u"едно", u"две", u"три", u"четири", u"пет", u"шест", u"седем", u"осем", u"девет", u"десет", 
					u"единайсет", u"дванайсет", u"тринайсет", u"четиринайсет", u"петнайсет", u"шестнайсет", u"седемнайсет", u"осемнайсет", u"деветнайсет"]
				if i < 20:
					s += temp[int(i % 20) - 1]
				else:
					s += temp[int(i % 10) - 1]
			if i == 0:
				s += u"нула"
			text = text.replace(str(i), s)
		
		return text
		
	@staticmethod
	def symbolsToText(text):
		symbols = { u"%": u" процента", u"°": u" градуса" }
		
		for (key, value) in symbols.items():
			text = text.replace(key, value)
			
		return text