#!/usr/bin/env
import sys, os, threading, subprocess
sys.path.append(os.path.join(os.getcwd(), "External"))
os.chdir("bin")

from External.flask import *
from Page import *
from MyHome import *

app = Flask(__name__)
myHome = MHome()


# TODO: login page
@app.route('/favicon.ico')
def favicon():
	return send_from_directory("", "MyHome.ico")

@app.route("/")
def index():
	data = request.form if request.method == "POST" else request.args
	if len(data) > 0:
		for arg in data:
			value = data[arg] == "True"
			myHome.systems[arg].enabled = value
		return redirect("/")
		
	return html(indexContent(myHome), True)
	
@app.route("/test")
def test():
	myHome.test()
	return redirect("/")

@app.route("/settings/<system>", methods=["GET", "POST"])
def settings(system):
	data = request.form if request.method == "POST" else request.args
	if len(data) > 0:
		for arg in data:
			if arg.endswith("[]"):
				value = str(data.getlist(arg))
				arg = arg[:-2]
			else:
				value = data[arg]
 			sys = myHome.systems[system]
			if hasattr(sys, arg):
				attrType = type(getattr(sys, arg))
				setattr(sys, arg, parse(value, attrType))
		return redirect("/")
		
	return html(settingsContent(myHome, system))
    
@app.route("/config", methods=["GET", "POST"])
def config():
	data = request.form if request.method == "POST" else request.args
	if len(data) > 0:
		for arg in data:
			value = data[arg]
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