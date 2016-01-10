from datetime import *

def getProperties(obj, includeParent=True):
	result = []
	items = dir(obj)
	for attr in items:
		attrType = type(getattr(obj, attr))
		if attr[0] == "_" or attr.istitle():
			continue
		if not includeParent and hasattr(obj.__class__.__bases__[0], attr):
			continue
		if (attrType is bool) or \
			(attrType is int) or \
			(attrType is float) or \
			(attrType is str) or \
			(attrType is datetime) or \
			(attrType is timedelta):
			result.append(attr)
	return result
	
def parse(value, type):
	if type is bool:
		return value == "True"
	elif type is int:
		return int(value)
	elif type is float:
		return float(value)
	elif type is str:
		return value
	elif type is datetime:
		return datetime.strptime(value, "%Y-%m-%d %H:%M:%S.%f")
	elif type is timedelta:
		return datetime.strptime(value, "%H:%M:%S") - datetime(1900, 1, 1)
	print type
	raise Exception("unknown type")