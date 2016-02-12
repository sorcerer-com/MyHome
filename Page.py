from Utils.Utils import *

def style():
	result = "body\n"
	result += "{\n"
	result += "	background: #C0A166;\n"
	result += "}\n"
	result += "a\n"
	result += "{\n"
	result += "	text-decoration: none;\n"
	result += "}\n"
	# title
	result += ".title\n"
	result += "{\n"
	result += "	display: table;\n"
	result += "	margin-bottom: 10px;\n"
	result += "}\n"
	result += ".title img\n"
	result += "{\n"
	result += "	display: table-cell;\n"
	result += "	width: 32px;\n"
	result += "	height: 32px;\n"
	result += "	padding: 10px;\n"
	result += "}\n"
	result += ".title h1\n"
	result += "{\n"
	result += "	display: table-cell;\n"
	result += "	vertical-align: middle;\n"
	result += "}\n"
	result += "h2.title\n"
	result += "{\n"
	result += "	margin: 0px 0px 10px 15px;\n"
	result += "}\n"
	# button
	result += ".button\n"
	result += "{\n"
	result += "	padding: 3px 5px;\n"
	result += "	color: black;\n"
	result += "	background: #ddd;\n"
	result += "	border-color: #fff #888 #888 #fff;\n"
	result += "	border-width: 2px;\n"
	result += "	border-style: solid;\n"
	result += "	margin: 3px;\n"
	result += "}\n"
	result += ".buttonContainer\n"
	result += "{\n"
	result += "	padding: 3px;\n"
	result += "	margin: 5px;\n"
	result += "}\n"
	result += ".red\n"
	result += "{\n"
	result += "	background: #d00;\n"
	result += "	border-color: #f00 #800 #800 #f00;\n"
	result += "}\n"
	result += ".green\n"
	result += "{\n"
	result += "	background: #0d0;\n"
	result += "	border-color: #0f0 #080 #080 #0f0;\n"
	result += "}\n"
	result += ".login\n"
	result += "{\n"
	result += "	font-size: large;\n"
	result += "	font-weight: bold;\n"
	result += "	padding: 5px 7px;\n"
	result += "	margin: 7px 5px;\n"
	result += "}\n"
	# system list
	result += "ul.system, .settings\n"
	result += "{\n"
	result += "	list-style-type: none;\n"
	result += "	padding: 0px 5px;\n"
	result += "}\n"
	result += ".system h2\n"
	result += "{\n"
	result += "	display: inline-block;\n"
	result += "	width: 150px;\n"
	result += "	margin: 5px 0px;\n"
	result += "}\n"
	# settings
	result += ".settings h3\n"
	result += "{\n"
	result += "	display: inline-block;\n"
	result += "	width: 200px;\n"
	result += "	margin: 3px 0px;\n"
	result += "}\n"
	return result
	
def script():
	result = "function submitForm(formName) {\n"
	result += "	document.getElementById(formName).submit()\n"
	result += "}\n"
	result += "function addItem(sender) {\n"
	result += "	var parent = sender.parentElement.parentElement\n"
	result += "	var index = Array.prototype.indexOf.call(parent.children, sender.parentElement)\n"
	result += "	var newItem = parent.children[index - 1].cloneNode(true)\n"
	result += "	parent.insertBefore(newItem, parent.children[index])\n"
	result += "}\n"
	result += "function removeItem(sender) {\n"
	result += "	var parent = sender.parentElement.parentElement\n"
	result += "	parent.parentElement.removeChild(parent)\n"
	result += "}\n"
	result += "function addText(sender, formName, inputName) {\n"
	result += "	var form = document.getElementById(formName)\n"
	result += "	var element = form.elements[inputName]\n"
	result += "	element.value += sender.text\n"
	result += "}\n"
	result += "function removeText(formName, inputName, count) {\n"
	result += "	var form = document.getElementById(formName)\n"
	result += "	var element = form.elements[inputName]\n"
	result += "	element.value = element.value.substr(0, element.value.length - count)\n"
	result += "}\n"
	return result

def html(content, autorefresh = False):
	result = "<!DOCTYPE html>\n"
	result += "<html>\n"
	result += "<head>\n"
	result += "<title>My Home</title>\n"
	if autorefresh:
		result += "<meta http-equiv='Refresh' content='10'>\n"
	result += "<meta name='viewport' content='initial-scale=1.0, width=device-width'/>\n"
	result += "\n"
	result += "<style>\n"
	result += style();
	result += "</style>\n"
	result += "\n"
	result += "<script type='text/javascript'>\n"
	result += script();
	result += "</script>\n"
	result += "</head>\n"
	result += "\n"
	result += "<body>\n"
	result += "\n"
	result += "<div class='title'>\n"
	result += "<img src='/favicon.ico'/>\n"
	result += "<h1>My Home</h1>\n"
	result += "</div>\n"
	result += "\n"
	result += content;
	result += "\n"
	result += "</body>\n"
	result += "</html>\n"
	return result
	
	
def indexContent(myHome):
	result = "<ul class='system'>\n"
	for (key, value) in myHome.systems.items():
		result += "<li>\n"
		result += "<h2>%s</h2>\n" % key
		if value.enabled:
			result += "<a class='button green' href='?%s=%s'>Enabled</a>\n" % (value.Name, False)
		else:
			result += "<a class='button red' href='?%s=%s'>Disabled</a>\n" % (value.Name, True)
		result += "<a class='button' href='/settings/%s'>Settings</a>\n" % value.Name
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
	
def mediaPlayerContent(mediaPlayer):
	result = "<h2 class='title'>Media Player</h2>\n"
	result += "<form id='form' action='' method='post'>\n"
	result += "<select name='play'>\n"
	for item in mediaPlayer._list:
		if mediaPlayer.getPlaying() == item:
			result += "<option value='%s' selected>%s</option>\n" %(item, item)
		else:
			result += "<option value='%s'>%s</option>\n" %(item, item)
	result += "</select>\n"
	result += "<div class='buttonContainer'>\n"
	if mediaPlayer.getPlaying() == "":
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
	result += property("rootPath", mediaPlayer.rootPath)
	result += "</ul>\n"
	result += "<a class='button' href='javascript:;' onclick='submitForm(\"form1\")'>Save</a>\n"
	result += "<a class='button' href='/'>Back</a>\n"
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