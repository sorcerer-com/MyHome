from datetime import *
from BaseSystem import *
from Services.InternetService import *

class ControlSystem(BaseSystem):
	Name = "Control"

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)

		self.prevDate = ""
		self.checkInterval = timedelta(minutes=1)
		self._nextTime = datetime.now()
		
	def update(self):
		BaseSystem.update(self)
		
		if datetime.now() < self._nextTime:
			return
		self._nextTime = datetime.now() + self.checkInterval
		
		try:
			res = InternetService.receiveEMails(date = self.prevDate)
		except: # timeout
			res = False
		if res == False or len(res) == 0:
			return
		
		for msg in reversed(res):
			if "My Home command" not in msg["subject"] or \
				Config.EMail not in msg["from"]:
				continue
				
			Logger.log("info", "Control System: command email received")
			if msg.is_multipart():
				for part in msg.walk():
					if part.get_content_type() == "text/plain":
						command = part.get_payload()
						break
			else:
				command = msg.get_payload()
			if "." in command:
				command = command.replace("MyHome.", "self._owner.")
				for name in self._owner.systems.keys():
					command = command.replace(name + ".", "self._owner.systems['%s']." % name)
			self._owner.event(self, "CommandReceived", command)
			
			try:
				exec(command)
				self._owner.event(self, "CommandExecuted", command)
			except Exception as e:
				Logger.log("error", "Control System: cannot execute '%s'" % command)
				Logger.log("exception", str(e))
			
		self.prevDate = res[0]["date"]
		self._owner.systemChanged = True
