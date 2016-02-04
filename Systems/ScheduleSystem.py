from datetime import *
from BaseSystem import *
from Utils.Utils import *

class ScheduleSystem(BaseSystem):
	Name = "Schedule"

	def __init__(self, owner):
		BaseSystem.__init__(self, owner)
		
		self._schedule = []
		temp = datetime.now()
		# start Security System every day at 10:00
		self._schedule.append([])
		self._schedule[-1].append(temp.replace(hour=10, minute=0, second=0, microsecond=0))
		self._schedule[-1].append(timedelta(hours=24))
		self._schedule[-1].append("Security.enabled=True")
		# stop Security System every day at 18:00
		self._schedule.append([])
		self._schedule[-1].append(temp.replace(hour=18, minute=0, second=0, microsecond=0))
		self._schedule[-1].append(timedelta(hours=24))
		self._schedule[-1].append("Security.enabled=False")
		# stop Security System every saturday at 10:00
		self._schedule.append([])
		self._schedule[-1].append(temp.replace(hour=10, minute=0, second=1, microsecond=0) + timedelta(5 - temp.weekday()))
		self._schedule[-1].append(timedelta(hours=24*7))
		self._schedule[-1].append("Security.enabled=False")
		# stop Security System every sunday at 10:00
		self._schedule.append([])
		self._schedule[-1].append(temp.replace(hour=10, minute=0, second=1, microsecond=0) + timedelta(6 - temp.weekday()))
		self._schedule[-1].append(timedelta(hours=24*7))
		self._schedule[-1].append("Security.enabled=False")
		# test My Home every 30 days at 20:00
		self._schedule.append([])
		self._schedule[-1].append(temp.replace(month=temp.month+1, day=1, hour=20, minute=0, second=0, microsecond=0))
		self._schedule[-1].append(timedelta(hours=24*30))
		self._schedule[-1].append("MyHome.test()")
		
		self._nextTime = datetime.now()
	
	@property
	def schedule(self):
		result = []
		for item in self._schedule:
			result.append("%s (%s) %s" %(string(item[0]), string(item[1]), string(item[2])))
		return result

	@schedule.setter
	def schedule(self, value):
		if self._schedule <> value:
			self._schedule = []
			for item in value:
				self._schedule.append([])
				item = item.split(" (")
				self._schedule[-1].append(parse(item[0], datetime))
				item = item[1].split(") ")
				self._schedule[-1].append(parse(item[0], timedelta))
				self._schedule[-1].append(item[1])

	def update(self):
		BaseSystem.update(self)
		
		if datetime.now() < self._nextTime:
			return
		
		self._schedule.sort(key=lambda x: x[0])
		toRemove = []
		for item in self._schedule:
			if datetime.now() > item[0]:
				command = item[2]
				if "." in command :
					name = item[2].split(".")[0]
					if name == "MyHome":
						command = item[2].replace(name + ".", "self._owner.")
					elif name in self._owner.systems:
						command = item[2].replace(name + ".", "self._owner.systems['%s']." % name)
				
				try:
					exec(command)
				except Exception as e:
					Logger.log("error", "Schedule System: cannot execute '%s'" % command)
					Logger.log("debug", str(e))

				if item[0] + item[1] == item[0]:
					toRemove.append(item)
				else:
					item[0] += item[1]
			else:
				break
		for item in toRemove:
			self._schedule.remove(item)
		self._schedule.sort(key=lambda x: x[0])
		
		if len(self._schedule) > 0:
			self._nextTime = self._schedule[0][0]
		else:
			self._nextTime += timedelta(seconds=1)
