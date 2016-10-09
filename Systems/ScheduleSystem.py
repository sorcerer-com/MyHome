from datetime import *
from BaseSystem import *
from Utils.Utils import *

class ScheduleSystem(BaseSystem):
	Name = "Schedule"

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self._schedule = []
		temp = datetime.now()
		
		self._schedule.append({})
		self._schedule[-1]["Name"] = "start Security System every day at 10:00"
		self._schedule[-1]["Time"] = temp.replace(hour=10, minute=0, second=0, microsecond=0)
		self._schedule[-1]["Repeat"] = timedelta(hours=24)
		self._schedule[-1]["Execute"] = "Security.enabled=True"
		self._schedule[-1]["Color"] = "rgba(0, 0, 255, 0.3)"
		
		self._schedule.append({})
		self._schedule[-1]["Name"] = "stop Security System every day at 18:00"
		self._schedule[-1]["Time"] = temp.replace(hour=18, minute=0, second=0, microsecond=0)
		self._schedule[-1]["Repeat"] = timedelta(hours=24)
		self._schedule[-1]["Execute"] = "Security.enabled=False"
		self._schedule[-1]["Color"] = "rgba(0, 0, 255, 0.3)"
		
		self._schedule.append({})
		self._schedule[-1]["Name"] = "stop Security System every saturday at 10:00"
		self._schedule[-1]["Time"] = temp.replace(hour=10, minute=0, second=1, microsecond=0) + timedelta(5 - temp.weekday())
		self._schedule[-1]["Repeat"] = timedelta(hours=24*7)
		self._schedule[-1]["Execute"] = "Security.enabled=False"
		self._schedule[-1]["Color"] = "rgba(0, 0, 255, 0.3)"
		
		self._schedule.append({})
		self._schedule[-1]["Name"] = "stop Security System every sunday at 10:00"
		self._schedule[-1]["Time"] = temp.replace(hour=10, minute=0, second=1, microsecond=0) + timedelta(6 - temp.weekday())
		self._schedule[-1]["Repeat"] = timedelta(hours=24*7)
		self._schedule[-1]["Execute"] = "Security.enabled=False"
		self._schedule[-1]["Color"] = "rgba(0, 0, 255, 0.3)"
		
		self._schedule.append({})
		self._schedule[-1]["Name"] = "test My Home every 30 days at 20:00"
		self._schedule[-1]["Time"] = temp.replace(month=temp.month+1, day=1, hour=20, minute=0, second=0, microsecond=0)
		self._schedule[-1]["Repeat"] = timedelta(hours=24*30)
		self._schedule[-1]["Execute"] = "MyHome.test()"
		self._schedule[-1]["Color"] = "rgba(0, 0, 255, 0.3)"
		
		self._nextTime = datetime.now()
		
	def loadSettings(self, configParser, data):
		BaseSystem.loadSettings(self, configParser, data)
		if len(data) ==  0:
			return
		self._schedule = []
		
		count = int(data[0])
		names = data[1].split(",")
		for i in range(0, count):
			self._schedule.append({})
			for j in range(0, len(names)):
				if names[j] == "Time":
					self._schedule[i][names[j]] = parse(data[2 + i * len(names) + j], datetime)
				elif names[j] == "Repeat":
					self._schedule[i][names[j]] = parse(data[2 + i * len(names) + j], timedelta)
				else:
					self._schedule[i][names[j]] = parse(data[2 + i * len(names) + j], str)

	def saveSettings(self, configParser, data):
		BaseSystem.saveSettings(self, configParser, data)
		if len(self._schedule) == 0:
			return
			
		data.append(len(self._schedule))
		data.append(",".join(self._schedule[0].keys()))
		for item in self._schedule:
			for (key, value) in item.items():
				data.append("%s" % string(value))

	def update(self):
		BaseSystem.update(self)
		
		if datetime.now() < self._nextTime:
			return
		
		self._schedule.sort(key=lambda x: x["Time"])
		toRemove = []
		for item in self._schedule:
			if datetime.now() > item["Time"]:
				command = item["Execute"]
				if "." in command:
					command = command.replace("MyHome.", "self._owner.")
					for name in self._owner.systems.keys():
						command = command.replace(name + ".", "self._owner.systems['%s']." % name)
				
				try:
					exec(command)
				except Exception as e:
					Logger.log("error", "Schedule System: cannot execute '%s'" % command)
					Logger.log("debug", str(e))
				self._owner.event(self, "CommandExecuted", command)

				if item["Time"] + item["Repeat"] == item["Time"]:
					toRemove.append(item)
				else:
					item["Time"] += item["Repeat"]
				self._owner.systemChanged = True
			else:
				break
		for item in toRemove:
			self._schedule.remove(item)
		self._schedule.sort(key=lambda x: x["Time"])
		
		if len(self._schedule) > 0:
			self._nextTime = self._schedule[0]["Time"]
		else:
			self._nextTime += timedelta(seconds=1)
