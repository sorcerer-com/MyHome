#!/usr/bin/env
import sys, os, threading, subprocess
sys.path.append(os.path.join(os.getcwd(), "External"))
os.chdir("bin")

from External.flask import *
from Page import *
from MyHome import *

logging.getLogger().info("")
app = Flask(__name__)
myHome = MHome()


# TODO: autostart(/home/pi/.bashrc)
@app.route('/favicon.ico')
def favicon():
	return send_from_directory("", "MyHome.ico")

@app.route("/")
def index():
	if ("password" not in session) or (session["password"] != Config.Password):
		return redirect("/login")
		
	data = request.form if request.method == "POST" else request.args
	if len(data) > 0:
		for arg in data:
			value = data[arg] == "True"
			myHome.systems[arg].enabled = value
		return redirect("/")
		
	return html(indexContent(myHome), False) # disable auto refresh for now
	
@app.route("/login", methods=["GET", "POST"])
def login():
	if Config.Password == "":
		session["password"] = Config.Password
		return redirect("/")
		
	data = request.form if request.method == "POST" else request.args
	if "password" in data: 
		if str(hash(data["password"])) == Config.Password:
			Logger.log("info", "LogIn: correct password")
			session["password"] = str(hash(data["password"]))
			return redirect("/")
		else:
			Logger.log("warning", "LogIn: invalid password")
			invalid = True
	else:
		invalid = False
		
	return html(loginContent(invalid))
	
@app.route("/log")
def log():
	if ("password" not in session) or (session["password"] != Config.Password):
		return redirect("/login")
	return html(logContent())
	
@app.route("/test")
def test():
	if ("password" not in session) or (session["password"] != Config.Password):
		return redirect("/login")
	myHome.test()
	return redirect("/")
    
@app.route("/config", methods=["GET", "POST"])
def config():
	return settings("Config")

@app.route("/settings/<systemName>", methods=["GET", "POST"])
def settings(systemName):
	if ("password" not in session) or (session["password"] != Config.Password):
		return redirect("/login")
		
	system = myHome.systems[systemName] if systemName <> "Config" else Config
	data = request.form if request.method == "POST" else request.args
	if len(data) > 0:
		for arg in data:
			if arg.endswith("[]"):
				value = str(data.getlist(arg))
				arg = arg[:-2]
			else:
				value = str(data[arg])
				
			if hasattr(system, arg):
				attrType = type(getattr(system, arg))
				setattr(system, arg, parse(value, attrType))
		return redirect("/")
		
	return html(settingsContent(system))
	
@app.route("/settings/MediaPlayer", methods=["GET", "POST"])
def mediaPlayer():
	if ("password" not in session) or (session["password"] != Config.Password):
		return redirect("/login")
		
	mediaPlayer = myHome.systems["MediaPlayer"]
	data = request.form if request.method == "POST" else request.args
	if len(data) > 0:
		if "play" in data:
			mediaPlayer.play(data["play"])
		if "action" in data and hasattr(mediaPlayer, data["action"]):
			getattr(mediaPlayer, data["action"])() # call function with set action name
		if "rootPath" in data:
			mediaPlayer.rootPath = str(data["rootPath"])
		return redirect("/settings/MediaPlayer")
		
	return html(mediaPlayerContent(mediaPlayer), False) # disable auto refresh for now, causing selection problem


def start():
	app.secret_key = u"\xf2N\x8a 8\xb1\xd9(&\xa6\x90\x12R\xf0\\\xe8\x1e\xf92\xa6AN\xed\xb3"
	app.permanent_session_lifetime = timedelta(minutes=30)
	app.run(debug=False, host='0.0.0.0')

if __name__ == "__main__":
	if len(sys.argv) > 1 and sys.argv[1] == "-service":
		start()
	else:		
		t = threading.Thread(target=start)
		t.daemon = True
		t.start()

		try:
			subprocess.call(["sensible-browser", "localhost:5000"], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
		except:
			t.join()
			
	myHome.__del__()