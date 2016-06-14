from Utils.Utils import *

def template(content, autorefresh = None):
	result = ""
	with open("../Page.html") as file:
		result = file.read()
		
	head = ""
	if autorefresh != None:
		head += "<meta http-equiv='Refresh' content='%s'>\n" % autorefresh
	result = result.replace("<head/>", head)
	result = result.replace("<content/>", content)
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
	result += "<a class='button' href='/test'>Test</a>\n"
	result += "<a class='button' href='/config'>Config</a>\n"
	return result
	
def loginContent(invalid):
	result = "<h2 class='title'>LogIn</h2>\n"
	result += "<form id='form' action='' method='post'>\n"
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
	
def logContent():
	result = "<h2 class='title'>Log</h2>\n"
	result += "<p>\n"
	#with open(Config.LogFileName, "r") as file:
	for line in Logger.data:
		if line.endswith("\n"):
			line = line[:-1]
		if "\terror\t" in line:
			result += "<div class='red'>%s</div>\n" % line
		else:
			result += "%s<br/>\n" % line
	result += "</p>\n"
	result += "<a class='button' href='/'>Back</a>\n"
	return result
	
def settingsContent(system):
	if system <> Config:
		result = "<h2 class='title'>%s Settings</h2>\n" % system.Name
	else:
		result = "<h2 class='title'>Config</h2>\n"
	result += "<form id='form' action='' method='post'>\n"
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
	result = "<h2 class='title'>Media Player</h2>\n"
	result += "<form id='form' action='' method='post'>\n"
	result += "<select name='play'>\n"
	for item in mediaPlayerSystem._list:
		if mediaPlayerSystem.getPlaying() == item:
			result += "<option value='%s' selected>%s</option>\n" %(item, item)
		else:
			result += "<option value='%s'>%s</option>\n" %(item, item)
	result += "</select>\n"
	result += "<div class='buttonContainer'>\n"
	if mediaPlayerSystem.getPlaying() == "":
		result += "<a class='button' href='javascript:;' onclick='submitForm(\"form\");'>Play</a>\n"
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
	
	result += "<form id='form1' action='' method='post'>\n"
	result += "<ul class='settings'>\n"
	result += property("rootPath", mediaPlayerSystem.rootPath)
	result += property("volume", mediaPlayerSystem.volume)
	result += "</ul>\n"
	result += "<a class='button' href='javascript:;' onclick='submitForm(\"form1\")'>Save</a>\n"
	result += "<a class='button' href='/'>Back</a>\n"
	result += "</form>\n"
	return result
	
def scheduleContent(scheduleSystem):
	result = "<h2 class='title'>Schedule</h2>\n"
	result += "<form id='form' action='' method='post'>\n"
	result += "<ul class='settings'>\n"
	for item in scheduleSystem.schedule:
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
	result += "<a class='button' href='javascript:;' onclick='submitForm(\"form\")'>Save</a>\n"
	result += "<a class='button' href='/'>Cancel</a>\n"
	result += "</form>\n"
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