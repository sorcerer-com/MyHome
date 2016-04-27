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
		except:
			res = False
		if res == False or len(res) == 0:
			return
		
		for msg in reversed(res):
			if "My Home command" not in msg["subject"] or \
				Config.EMail not in msg["from"]:
				continue
				
			Logger.log("info", "Control System: command email received")
			command = msg.get_payload()
			if "." in command:
				name = command.split(".")[0]
				if name == "MyHome":
					command = command.replace(name + ".", "self._owner.")
				elif name in self._owner.systems:
					command = command.replace(name + ".", "self._owner.systems['%s']." % name)
			
			try:
				exec(command)
			except Exception as e:
				Logger.log("error", "Control System: cannot execute '%s'" % command)
				Logger.log("debug", str(e))
			
		self.prevDate = res[0]["date"]
		self._owner.systemChanged = True
