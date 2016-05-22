from datetime import *
from BaseSystem import *
from Utils.Utils import *

class ScheduleSystem(BaseSystem):
	Name = "Schedule"

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self.schedule = []
		temp = datetime.now()
		
		self.schedule.append({})
		self.schedule[-1]["Name"] = "start Security System every day at 10:00"
		self.schedule[-1]["Time"] = temp.replace(hour=10, minute=0, second=0, microsecond=0)
		self.schedule[-1]["Repeat"] = timedelta(hours=24)
		self.schedule[-1]["Execute"] = "Security.enabled=True"
		self.schedule[-1]["Color"] = "rgba(0, 0, 255, 0.3)"
		
		self.schedule.append({})
		self.schedule[-1]["Name"] = "stop Security System every day at 18:00"
		self.schedule[-1]["Time"] = temp.replace(hour=18, minute=0, second=0, microsecond=0)
		self.schedule[-1]["Repeat"] = timedelta(hours=24)
		self.schedule[-1]["Execute"] = "Security.enabled=False"
		self.schedule[-1]["Color"] = "rgba(0, 0, 255, 0.3)"
		
		self.schedule.append({})
		self.schedule[-1]["Name"] = "stop Security System every saturday at 10:00"
		self.schedule[-1]["Time"] = temp.replace(hour=10, minute=0, second=1, microsecond=0) + timedelta(5 - temp.weekday())
		self.schedule[-1]["Repeat"] = timedelta(hours=24*7)
		self.schedule[-1]["Execute"] = "Security.enabled=False"
		self.schedule[-1]["Color"] = "rgba(0, 0, 255, 0.3)"
		
		self.schedule.append({})
		self.schedule[-1]["Name"] = "stop Security System every sunday at 10:00"
		self.schedule[-1]["Time"] = temp.replace(hour=10, minute=0, second=1, microsecond=0) + timedelta(6 - temp.weekday())
		self.schedule[-1]["Repeat"] = timedelta(hours=24*7)
		self.schedule[-1]["Execute"] = "Security.enabled=False"
		self.schedule[-1]["Color"] = "rgba(0, 0, 255, 0.3)"
		
		self.schedule.append({})
		self.schedule[-1]["Name"] = "test My Home every 30 days at 20:00"
		self.schedule[-1]["Time"] = temp.replace(month=temp.month+1, day=1, hour=20, minute=0, second=0, microsecond=0)
		self.schedule[-1]["Repeat"] = timedelta(hours=24*30)
		self.schedule[-1]["Execute"] = "MyHome.test()"
		self.schedule[-1]["Color"] = "rgba(0, 0, 255, 0.3)"
		
		self._nextTime = datetime.now()
		
	def loadSettings(self, configParser):
		if not configParser.has_section(self.Name):
			return
			
		items = configParser.items(self.Name)
		for (name, value) in items:
			temp = parse(value, list)
			for i in range(0, len(temp)):
				if len(self.schedule) <= i:
					self.schedule.append({})
				if name == "Time":
					value = parse(temp[i], datetime)
				elif name == "Repeat":
					value = parse(temp[i], timedelta)
				else:
					value = parse(temp[i], str)
				self.schedule[i][name] = value

	def saveSettings(self, configParser):
		configParser.add_section(self.Name)
		
		temp = {}
		for item in self.schedule:
			for (key, value) in item.items():
				if key not in temp:
					temp[key] = []
				temp[key].append(value)
		for (key, value) in temp.items():
			configParser.set(self.Name, key, string(value))

	def update(self):
		BaseSystem.update(self)
		
		if datetime.now() < self._nextTime:
			return
		
		self.schedule.sort(key=lambda x: x["Time"])
		toRemove = []
		for item in self.schedule:
			if datetime.now() > item["Time"]:
				command = item["Execute"]
				if "." in command:
					name = command.split(".")[0]
					if name == "MyHome":
						command = command.replace(name + ".", "self._owner.")
					elif name in self._owner.systems:
						command = command.replace(name + ".", "self._owner.systems['%s']." % name)
				
				try:
					exec(command)
				except Exception as e:
					Logger.log("error", "Schedule System: cannot execute '%s'" % command)
					Logger.log("debug", str(e))

				if item["Time"] + item["Repeat"] == item["Time"]:
					toRemove.append(item)
				else:
					item["Time"] += item["Repeat"]
					self._owner.systemChanged = True
			else:
				break
		for item in toRemove:
			self.schedule.remove(item)
		self.schedule.sort(key=lambda x: x["Time"])
		
		if len(self.schedule) > 0:
			self._nextTime = self.schedule[0]["Time"]
		else:
			self._nextTime += timedelta(seconds=1)
