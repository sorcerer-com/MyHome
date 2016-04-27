import re
from datetime import *
from functools import wraps
from multiprocessing import Process, Queue
from Logger import *

def getProperties(obj, baseProps = False):
	result = []
	items = dir(obj)
	for attr in items:
		attrType = type(getattr(obj, attr))
		if attr.startswith("_") or attr.istitle():
			continue
		if not baseProps and hasattr(obj.__class__.__bases__[0], attr):
			continue
		if (attrType is bool) or \
			(attrType is int) or \
			(attrType is float) or \
			(attrType is str) or \
			(attrType is datetime) or \
			(attrType is timedelta) or \
			(attrType is list):
			result.append(attr)
	return result
	
def string(value):
	try:
		valueType = type(value)
		if valueType is datetime:
			return value.strftime("%Y-%m-%d %H:%M:%S")
		elif valueType is timedelta:
			value = datetime(1900, 1, 1) + value
			return "%02d-%02d-%02d %02d:%02d:%02d" % (value.year - 1900, value.month - 1, value.day - 1, value.hour, value.minute, value.second)
		elif valueType is list:
			result = "["
			for obj in value:
				if type(obj) is list:
					result += "%s, " % string(obj)
				else:
					result += "\"%s\", " % string(obj)
			if len(result) > 2:
				result = result[:-2]
			return result + "]"
	except Exception as e:
		Logger.log("error", "Utils: cannot convert '%s' to string" % value)
		Logger.log("debug", str(e))
	return str(value)
		
def parse(value, valueType):
	try:
		if valueType is bool:
			return value == "True"
		elif valueType is int:
			return int(value)
		elif valueType is float:
			return float(value)
		elif valueType is str:
			return value
		elif valueType is datetime:
			return datetime.strptime(value, "%Y-%m-%d %H:%M:%S")
		elif valueType is timedelta:
			value = re.split("-| |:", value)
			return datetime(1900 + int(value[0]), 1 + int(value[1]), 1 + int(value[2]), int(value[3]), int(value[4]), int(value[5])) - datetime(1900, 1, 1)
		elif valueType is list:
			return eval(value)
		raise Exception("parse unknown type: " + str(valueType))
	except Exception as e:
		Logger.log("error", "Utils: cannot convert '%s' to %s" % (value, valueType))
		Logger.log("debug", str(e))
		return valueType()
		
def timeout(timeout):
	def wrap_function(func):

		@wraps(func)
		def __wrapper(*args, **kwargs):
			def queue_wrapper(args, kwargs):
				q.put(func(*args, **kwargs))
 
			q = Queue()
			p = Process(target=queue_wrapper, args=(args, kwargs))
			p.start()
			p.join(float(timeout) / 1000)
			if p.is_alive():
				p.terminate()
				p.join()
				raise Exception("Timeout Exception")
			p.terminate()
			return q.get()
		return __wrapper
	return wrap_function