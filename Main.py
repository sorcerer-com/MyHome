#!/usr/bin/env
import sys, os, threading, subprocess
sys.path.append(os.path.join(os.getcwd(), "External"))
os.chdir("bin")

from External.flask import *
from MyHome import *
from Utils.Utils import *

app = Flask(__name__)
myHome = MHome()

def style():
	result = "body\n"
	result += "{\n"
	result += "background: #C0A166;\n"
	result += "}\n"
	# title
	result += ".title\n"
	result += "{\n"
	result += "display: table;\n"
	result += "margin-bottom: 10px;\n"
	result += "}\n"
	result += ".title img\n"
	result += "{\n"
	result += "display: table-cell;\n"
	result += "width: 32px;\n"
	result += "height: 32px;\n"
	result += "padding: 10px;\n"
	result += "}\n"
	result += ".title h1\n"
	result += "{\n"
	result += "display: table-cell;\n"
	result += "vertical-align: middle;\n"
	result += "}\n"
	result += "h2.title\n"
	result += "{\n"
	result += "margin: 0px 0px 10px 15px;\n"
	result += "}\n"
	# system list
	result += "ul.system, .settings\n"
	result += "{\n"
	result += "list-style-type: none;\n"
	result += "padding: 0px 10px;\n"
	result += "}\n"
	result += ".system h2\n"
	result += "{\n"
	result += "display: inline-block;\n"
	result += "width: 150px;\n"
	result += "margin: 0px;\n"
	result += "}\n"
	result += ".button\n"
	result += "{\n"
	result += "text-decoration: none;\n"
	result += "padding: 3px 5px;\n"
	result += "color: black;\n"
	result += "background: #ddd;\n"
	result += "border-color: #fff #888 #888 #fff;\n"
	result += "border-width: 2px;\n"
	result += "border-style: solid;\n"
	result += "}\n"
	result += ".red\n"
	result += "{\n"
	result += "background: #d00;\n"
	result += "border-color: #f00 #800 #800 #f00;\n"
	result += "}\n"
	result += ".green\n"
	result += "{\n"
	result += "background: #0d0;\n"
	result += "border-color: #0f0 #080 #080 #0f0;\n"
	result += "}\n"
	# settings
	result += ".settings h3\n"
	result += "{\n"
	result += "display: inline-block;\n"
	result += "width: 200px;\n"
	result += "margin: 0px;\n"
	result += "}\n"
	result += "a.settings\n"
	result += "{\n"
	result += "cursor: pointer;\n"
	result += "margin: 5px;\n"
	result += "}\n"
	return result

def html(content):
	result = "<!DOCTYPE html>\n"
	result += "<html>\n"
	result += "<head>\n"
	result += "<title>My Home</title>\n"
	result += "<meta http-equiv='Refresh' content='10'>\n"
	result += "<meta name='viewport' content='initial-scale=1.0, width=device-width'/>\n"
	result += "<style>\n"
	result += style();
	result += "</style>\n"
	result += "</head>\n"
	result += "\n"
	result += "<body>\n"
	result += "<div class='title'>\n"
	result += "<img src='/favicon.ico'/>\n"
	result += "<h1>My Home</h1>\n"
	result += "</div>\n"
	result += content;
	result += "</body>\n"
	result += "</html>\n"
	return result
	
def indexContent():
	result = "<ul class='system'>\n"
	for (key, value) in myHome.systems.items():
		result += "<li>\n"
		result += "<h2>%s</h2>\n" % key
		if value.enabled:
			result += "<a class='button green' href='/?%s=%s'>Enabled</a>\n" % (value.Name, False)
		else:
			result += "<a class='button red' href='/?%s=%s'>Disabled</a>\n" % (value.Name, True)
		result += "<a class='button' href='/settings/%s'>Settings</a>\n" % value.Name
		result += "</li>\n"
	result += "</ul>\n"
	result += "<a class='button' href='/config'>Config</a>\n"
	return result
	
def settingsContent(system):
	result = "<h2 class='title'>%s Settings</h2>\n" % system
	result += "<form id='form' action='/settings/%s' method='get'>\n" % system
	result += "<ul class='settings'>\n"
	items = getProperties(myHome.systems[system], False)
	for prop in items:
		value = getattr(myHome.systems[system], prop)
		result += "<li>\n"
		result += "<h3>%s: </h3>" % prop
		result += "<input type='text' name='%s' value='%s'\n" % (prop, value)
		result += "<li/>\n"
	result += "</ul>\n"
	result += "<a class='button settings' onclick=\"document.getElementById('form').submit()\">Save</a>"
	result += "<a class='button' href='/'>Cancel</a>"
	result += "</form>\n"
	return result
	
def configContent():
	result = "<h2 class='title'>Config</h2>\n"
	result += "<form id='form' action='/config' method='get'>\n"
	result += "<ul class='settings'>\n"
	items = dir(Config)
	for prop in items:
		value = getattr(Config, prop)
		if not ((type(value) is str) and (prop[0] <> "_")):
			continue
		result += "<li>\n"
		result += "<h3>%s: </h3>" % prop
		result += "<input type='text' name='%s' value='%s'\n" % (prop, value)
		result += "<li/>\n"
	result += "</ul>\n"
	result += "<a class='button settings' onclick=\"document.getElementById('form').submit()\">Save</a>"
	result += "<a class='button' href='/'>Cancel</a>"
	result += "</form>\n"
	return result


@app.route('/favicon.ico')
def favicon():
	return send_from_directory("", "MyHome.ico")

@app.route("/", methods=["GET"])
def index():
	if len(request.args) > 0:
		for arg in request.args:
			value = request.args.get(arg) == "True"
			myHome.systems[arg].enabled = value
		return redirect("/")
	return html(indexContent())

@app.route("/settings/<system>")
def settings(system):
	if len(request.args) > 0:
		sys = myHome.systems[system]
		for arg in request.args:
			value = request.args.get(arg)
			if hasattr(sys, arg):
				attrType = type(getattr(sys, arg))
				setattr(sys, arg, parse(value, attrType))
		return redirect("/")
	return html(settingsContent(system))
    
@app.route("/config")
def config():
	if len(request.args) > 0:
		for arg in request.args:
			value = request.args.get(arg)
			if hasattr(Config, arg):
				setattr(Config, arg, str(value))
		return redirect("/")
	return html(configContent())


def start():
	app.run(debug=False, host='0.0.0.0')

if __name__ == "__main__":
	t = threading.Thread(target=start)
	t.daemon = True
	t.start()

	subprocess.call(["sensible-browser", "localhost:5000"], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
	myHome.__del__()