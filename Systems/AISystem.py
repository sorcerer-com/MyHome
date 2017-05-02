# -*- coding: utf-8 -*- 
import warnings, threading, random, re
from gtts import gTTS
from BaseSystem import *
from Services.PCControlService import *
from Services.InternetService import *
from Utils.Utils import *

class AISystem(BaseSystem):
	Name = "AI"
	VoiceVolume = -3
	_LowConfidenceResponses = [u"Моля", u"Не ви чух", u"Може ли да повторите"]
	_UnknownVoiceCommandResponses = [u"Не ви разбирам", u"Не знам какво значи това", u"Какво значи това"]

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self._owner.event += lambda s, e, d=None: self._onEventReceived(s, e, d)
		
		self._enabled = False
		
		self._commands = []
		self._voiceCommands = {}
		self._lastVoiceCommand = (u"", u"")
		
		self._commands.append([u"result = u'%s и %s' % (datetime.now().hour % 12, datetime.now().minute)"])
		self._commands.append([u"weather = InternetService.getWeather()[0]", "{3}"])
		self._commands.append([u"weather = InternetService.getWeather()[1]", "{3}"])
		self._commands.append([u"if weather == False: result = u'В момента не мога да кажа какво ще е времето'",
							   u"else:",
							   u"	result  = u'Времето ще е %s. ' % weather['condition'].replace(u', ', u' с ')",
							   u"	result += u'Минималната температура ще е %s градуса, максималната %s. ' % (weather['minTemp'], weather['maxTemp'])",
							   u"	result += u'Вятарът ще е %s от %s. ' % (weather['wind'].split(u', ')[1], weather['wind'].split(u', ')[0])",
							   u"	if weather['rainProb'] > 30: result += u'Има %s процента вероятност за дъжд с интензитет %s мм. ' % (weather['rainProb'], weather['rainAmount'])",
							   u"	if weather['stormProb'] > 30: result += u'Вероятността за буря е %s процента. ' % weather['stormProb']",
							   u"	result += u'Oблачността ще е %s процента. ' % weather['cloudiness']"])
		
		self._voiceCommands[u"колко е часа"] = 0
		self._voiceCommands[u"какво ще е времето днес"] = 1
		self._voiceCommands[u"какво ще е времето утре"] = 2
		
	def loadSettings(self, configParser, data):
		BaseSystem.loadSettings(self, configParser, data)
		if len(data) == 0:
			return
		self._commands = []
		self._voiceCommands = {}
		
		ptr = 0
		count = int(data[ptr])
		ptr += 1
		for i in range(0, count):
			self._commands.append([])
			count2 = int(data[ptr])
			ptr += 1
			for j in range(0, count2):
				self._commands[i].append(data[ptr])
				ptr += 1
				
		count = int(data[ptr])
		ptr += 1
		for i in range(0, count):
			self._voiceCommands[data[ptr]] = int(data[ptr + 1])
			ptr += 2

	def saveSettings(self, configParser, data):
		BaseSystem.saveSettings(self, configParser, data)
		
		data.append(len(self._commands))
		for command in self._commands:
			data.append(len(command))
			for cmd in command:
				data.append(cmd)
				
		data.append(len(self._voiceCommands))
		for (key, value) in self._voiceCommands.items():
			data.append(key)
			data.append(value)

	def _onEnabledChanged(self):
		BaseSystem._onEnabledChanged(self)

	def update(self):
		BaseSystem.update(self)
		
		
	def _onEventReceived(self, sender, event, data):
		if not self._enabled:
			return
		Logger.log("debug", "AI System: %s %s %s" % (sender.Name, event, data))
		
		text = ""
		if event == "AlarmActivated":
			text = u"Кой е там?"
			
		if text != "":
			self._lastVoiceCommand = (u"", text)
			AISystem.say(text)
			Logger.log("debug", "AI System: " + text)
		
	def processVoiceCommand(self, transcript, confidence):
		Logger.log("debug", "AI System: " + transcript + " " + str(confidence))
		
		result = ""
		# too low confidence
		if confidence < 0.5:
			result = random.choice(AISystem._LowConfidenceResponses)
		# unknown command
		elif transcript not in self._voiceCommands.keys():
			explanation = False
			if self._lastVoiceCommand[1] in AISystem._UnknownVoiceCommandResponses and transcript.startswith(u"това е като "):
				vCommand = transcript[len(u"това е като "):]
				if vCommand in self._voiceCommands.keys(): # found explanation
					self._voiceCommands[self._lastVoiceCommand[0]] = self._voiceCommands[vCommand]
					result = u"Разбирам"
					self._owner.systemChanged = True
					explanation = True
			if not explanation:
				result = random.choice(AISystem._UnknownVoiceCommandResponses)
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
		
		self._lastVoiceCommand = (transcript, result)
		if result != "":
			AISystem.say(result)
			Logger.log("debug", "AI System: " + result)
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
		nums = [s for s in re.findall(ur"[-+]?\d*\.?\d+", text)]
		for num in sorted(nums, reverse=True):
			s = u""
			for n in num.split('.'):
				if len(s) > 0:
					s += u" цяло и "
				if float(n) < 0:
					s += u"минус "
				absNum = abs(float(n))
				if absNum >= 100:
					continue
				if absNum >= 20:
					temp = [u"двайсет", u"трийсет", u"четиресет", u"педесет", u"шейсет", u"седемдесет", u"осемдесет", u"деведесет"]
					s += temp[int(absNum / 10) - 2]
					if (absNum % 10) != 0:
						s += u" и "
				if absNum >= 0 and (absNum == 10 or absNum % 10 != 0):
					temp = [u"едно", u"две", u"три", u"четири", u"пет", u"шест", u"седем", u"осем", u"девет", u"десет", 
						u"единайсет", u"дванайсет", u"тринайсет", u"четиринайсет", u"петнайсет", u"шестнайсет", u"седемнайсет", u"осемнайсет", u"деветнайсет"]
					if absNum < 20:
						s += temp[int(absNum % 20) - 1]
					else:
						s += temp[int(absNum % 10) - 1]
				if absNum == 0:
					s += u"нула"
			text = text.replace(str(num), s)
		
		return text
		
	@staticmethod
	def symbolsToText(text):
		symbols = { u"%": u" процента", u"°": u" градуса" }
		
		for (key, value) in symbols.items():
			text = text.replace(key, value)
			
		return text