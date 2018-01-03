import collections
from datetime import datetime

class Sensor(object):

	def __init__(self, name):
		self.Name = name
		
		self._data = collections.OrderedDict() # subNames / (time / value)
		
	
	@property
	def subNames(self):
		return self._data.keys()
		
		
	def addValue(self, time, subName, value):
		if subName not in self._data:
			self._data[subName] = {}
		self._data[subName][time] = value

	def archiveData(self):
		for subName in self._data.keys():
			keys = sorted(self._data[subName].keys())
			idx = 0
			while idx < len(keys):
				time = keys[idx]
				
				times = []
				if (datetime.now() - time).days > 365: # for older then 365 days, delete it
					del self._data[subName][time]
				elif (datetime.now() - time).days > 5: # for older then 5 days, save only 1 per day
					for j in range(idx, len(keys)):
						if keys[j].day == time.day and (keys[j] - time).days < 1:
							times.append(keys[j])
						else:
							break
				elif (datetime.now() - time).days >= 1: # for older then 24 hour, save only 1 per hour
					for j in range(idx, len(keys)):
						if keys[j].hour == time.hour and (keys[j] - time).seconds < 1 * 3600: # less then hour
							times.append(keys[j])
						else:
							break
				
				if len(times) > 1:
					values = [self._data[subName][t] for t in times]
					newValue = None
					if type(values[0]) is bool:
						newValue = len([v for v in values if v]) >= float(len(values)) / 2 # if True values are more then False
					elif type(values[0]) is int:
						newValue = int(round(sum(values) / float(len(values))))
					elif type(values[0]) is float:
						newValue = sum(values) / float(len(values))
					
					for t in times:
						del self._data[subName][t]
					self._data[subName][times[0].replace(minute=0, second=0, microsecond=0)] = newValue
					idx += len(times)
				else:
					idx += 1
		
	def getLatestValue(self, subName):
		keys = sorted(self._data[subName].keys())
		return self._data[subName][keys[-1]]
				
	def getLatestData(self):
		result = []
		for subName in self.subNames:
			temp = self.getLatestValue(subName)
			result.append(round(float(temp), 2))
		return result
		
	def getData(self, subName, minTime, maxTime):
		result = {}
		for (time, value) in self._data[subName].items():
			if time >= minTime and time < maxTime:
				result[time] = round(value, 2)
		return result