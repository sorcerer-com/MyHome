from Utils.Utils import *

def template(content, title):
	result = ""
	with open("../Page.html") as file:
		result = file.read()
		
	if title != None:
		content = "<h2 class='title'>" + title + "</h2>\n" + content
	result = result.replace("<content/>", content)
	return result


def loginContent(invalid):
	result = "<form id='form' action='' method='post'>\n"
	if not invalid:
		result += "<input type='password' name='password' autofocus/>\n"
	else:
		result += "<input type='password' name='password' class='red' autofocus/>\n"
	result += "<div class='buttonContainer login'>\n"
	result += "<a class='button login' href='javascript:;' onclick='addText(this, \"form\", \"password\")'>1</a>\n"
	result += "<a class='button login' href='javascript:;' onclick='addText(this, \"form\", \"password\")'>2</a>\n"
	result += "<a class='button login' href='javascript:;' onclick='addText(this, \"form\", \"password\")'>3</a>\n"
	result += "</div>\n"
	result += "<div class='buttonContainer login'>\n"
	result += "<a class='button login' href='javascript:;' onclick='addText(this, \"form\", \"password\")'>4</a>\n"
	result += "<a class='button login' href='javascript:;' onclick='addText(this, \"form\", \"password\")'>5</a>\n"
	result += "<a class='button login' href='javascript:;' onclick='addText(this, \"form\", \"password\")'>6</a>\n"
	result += "</div>\n"
	result += "<div class='buttonContainer login'>\n"
	result += "<a class='button login' href='javascript:;' onclick='addText(this, \"form\", \"password\")'>7</a>\n"
	result += "<a class='button login' href='javascript:;' onclick='addText(this, \"form\", \"password\")'>8</a>\n"
	result += "<a class='button login' href='javascript:;' onclick='addText(this, \"form\", \"password\")'>9</a>\n"
	result += "</div>\n"
	result += "<div class='buttonContainer login'>\n"
	result += "<a class='button login' href='javascript:;' onclick='removeText(\"form\", \"password\", 1)'><</a>\n"
	result += "<a class='button login' href='javascript:;' onclick='addText(\"form\", \"password\", this)'>0</a>\n"
	result += "<a class='button login' href='javascript:;' onclick='submitForm(\"form\")'>></a>\n"
	result += "<div>\n"
	result += "</form>\n"
	return result
	
def indexContent(myHome):
	result = "<ul class='system'>\n"
	systemNames = myHome.systems.keys()
	systemNames.sort()
	for name in systemNames:
		result += "<li>\n"
		result += "<h2>%s</h2>\n" % name
		if myHome.systems[name].enabled:
			result += "<a class='button green' href='?%s=%s'>Enabled</a>\n" % (name, False)
		else:
			result += "<a class='button red' href='?%s=%s'>Disabled</a>\n" % (name, True)
		result += "<a class='button' href='/settings/%s'>Settings</a>\n" % name
		result += "</li>\n"
	result += "</ul>\n"
	result += "<a class='button' href='/log'>Log</a>\n"
	result += "<a class='button' href='/config'>Config</a>\n"
	result += "<a class='button' href='/test'>Test</a>\n"
	result += "<a class='button' href='/restart'>Restart</a>\n"
	return result
	
def logContent():
	result = "<a class='button' href='/'>Back</a>\n"
	result += "<pre>\n"
	#with open(Config.LogFileName, "r") as file:
	for line in reversed(Logger.data):
		if line.endswith("\n"):
			line = line[:-1]
		if "\terror\t" in line:
			result += "<div class='red'>%s</div>\n" % line
		else:
			result += "%s\n" % line
	result += "</pre>\n"
	return result
	
def settingsContent(system):
	result = "<form id='form' action='' method='post'>\n"
	result += "<ul class='settings'>\n"
	items = getProperties(system) if system <> Config else Config.list()
	for prop in items:
		value = getattr(system, prop)
		result += property(prop, value)
	result += "</ul>\n"
	result += "<a class='button' href='javascript:;' onclick='submitForm(\"form\")'>Save</a>\n"
	result += "<a class='button' href='/'>Cancel</a>\n"
	result += "</form>\n"
	return result
	
def mediaPlayerContent(mediaPlayerSystem):
	result = "<form id='form1' action='' method='post'>\n"
	result += "<select name='play'>\n"
	for item in mediaPlayerSystem._list:
		if mediaPlayerSystem.getPlaying() == item:
			result += "<option value='%s' selected>%s</option>\n" %(item, item)
		else:
			result += "<option value='%s'>%s</option>\n" %(item, item)
	result += "</select>\n"
	result += "<div class='buttonContainer'>\n"
	if mediaPlayerSystem.getPlaying() == "":
		result += "<a class='button' href='javascript:;' onclick='submitForm(\"form1\");'>Play</a>\n"
	else:
		result += "<a class='button' href='?action=stop'>Stop</a>\n"
	result += "<a class='button' href='?action=pause'>Pause</a>\n"
	result += "<a class='button' href='?action=volumeDown'> - </a>\n"
	result += "<a class='button' href='?action=volumeUp'> + </a>\n"
	result += "<a class='button' href='?action=seekBack'> < </a>\n"
	result += "<a class='button' href='?action=seekForward'> > </a>\n"
	result += "<a class='button' href='?action=seekBackFast'> << </a>\n"
	result += "<a class='button' href='?action=seekForwardFast'> >> </a>\n"
	result += "</div>\n"
	result += "</form>\n"
	
	result += settingsContent(mediaPlayerSystem)
	return result
	
def scheduleContent(scheduleSystem):
	result = "<form id='form1' action='' method='post'>\n"
	result += "<ul class='settings'>\n"
	for item in scheduleSystem._schedule:
		result += "<details>\n"
		tooltip = "%s (%s)" % (string(item["Time"]), string(item["Repeat"]))
		result += "<summary style='background-color: %s' title='%s'>%s</summary>\n" % (item["Color"], tooltip, item["Name"])
		for (key, value) in item.items():
			result += property(key, value)
		result += "<div class='buttonContainer'>\n"
		result += "<a class='button' href='javascript:;' onclick='removeItem(this)'>-</a>\n"
		result += "</div>\n"
		result += "</details>\n"
	result += "<div class='buttonContainer'>\n"
	result += "<a class='button' href='javascript:;' onclick='addItem(this)'>+</a>\n"
	result += "</div>\n"
	result += "</ul>\n"
	result += "<a class='button' href='javascript:;' onclick='submitForm(\"form1\")'>Save</a>\n"
	result += "<a class='button' href='/'>Cancel</a>\n"
	result += "</form>\n"
	return result

def sensorsContent(sensorsSystem):
	result = ""
	for i in range(0, len(sensorsSystem.sensorTypes)):
		result += "<details open>\n"
		result += "<summary>%s</summary>\n" % sensorsSystem.sensorTypes[i]
		data = {key: value[i] for (key, value) in sensorsSystem._data.items() if (datetime.now() - key).days < 1}
		if len(data) > 0 and type(data[data.keys()[0]]) is tuple:
			temp = {key: value[0] for (key, value) in data.items() if value is not None}
			result += chart("canvas0" + str(i), 300, 150, temp, True, False) + "<br/>"
			data = {key: value[1] for (key, value) in data.items() if value is not None}
		else:
			data = {key: value for (key, value) in data.items() if value is not None}
		result += chart("canvas1" + str(i), 300, 150, data, True, False) + "<br/>"
		result += "</details>\n"
	result += "<details>\n"
	result += "<summary>Archive</summary>\n"
	for i in range(0, len(sensorsSystem.sensorTypes)):
		result += "%s - 10 days<br/>\n" % sensorsSystem.sensorTypes[i]
		data = {key: value[i] for (key, value) in sensorsSystem._data.items() if (datetime.now() - key).days >= 1 and (datetime.now() - key).days <= 10}
		if len(data) > 0 and type(data[data.keys()[0]]) is tuple:
			temp = {key: value[0] for (key, value) in data.items() if value is not None}
			result += chart("canvas2" + str(i), 300, 150, temp, False, True) + "<br/>"
			data = {key: value[1] for (key, value) in data.items() if value is not None}
		else:
			data = {key: value for (key, value) in data.items() if value is not None}
		result += chart("canvas3" + str(i), 300, 150, data, False, True) + "<br/>"
	for i in range(0, len(sensorsSystem.sensorTypes)):
		result += "%s - Older<br/>\n" % sensorsSystem.sensorTypes[i]
		data = {key: value[i] for (key, value) in sensorsSystem._data.items() if (datetime.now() - key).days > 10}
		if len(data) > 0 and type(data[data.keys()[0]]) is tuple:
			temp = {key: value[0] for (key, value) in data.items() if value is not None}
			result += chart("canvas4" + str(i), 300, 150, temp, False, True) + "<br/>"
			data = {key: value[1] for (key, value) in data.items() if value is not None}
		else:
			data = {key: value for (key, value) in data.items() if value is not None}
		result += chart("canvas5" + str(i), 300, 150, data, False, True) + "<br/>"
	result += "</details>\n"
	
	result += settingsContent(sensorsSystem)
	return result
	
		
def property(name, value, item = False):
	result = ""
	if type(value) is list:
		if len(value) == 0:
			value.append("")
		for i in range(0, len(value)):
			result += "<div>\n" + property("%s[]" % name, value[i], True) + "</div>\n"
			
		result += "<div class='buttonContainer'>\n"
		result += "<a class='button' href='javascript:;' onclick='addItem(this)'>+</a>\n"
		if item:
			result += "<a class='button' href='javascript:;' onclick='removeItem(this)'>-</a>\n"
		result += "</div>\n"
	else:
		result += "<li>\n"
		result += "<h3>%s: </h3>\n" % name
		result += "<input type='text' name='%s' value='%s' title ='%s'/>\n" % (name, string(value), string(value))
		if item:
			result += "<a class='button' href='javascript:;' onclick='removeItem(this)'>-</a>\n"
		result += "</li>\n"
	return result
	
def chart(name, width, height, data, showValues, showAxis):
	result = "<canvas id='%s' width='%s' height='%s'></canvas>\n" % (name, width, height)
	result += "<script type='text/javascript'>\n"
	result += "// %s\n" % len(data)
	keys = sorted(data.keys())
	values = [string(int(data[key])) for key in keys if data[key] is not None]
	result += "var values = [%s];\n" % (",".join(values))
	names = [("'%s'" % string(key)) for key in keys if data[key] is not None]
	result += "var names = [%s];\n" % (",".join(names))
	result += "drawLineChart('%s', values, names, %s, %s);\n" % (name, str(showValues).lower(), str(showAxis).lower())
	result += "</script>\n"
	return result