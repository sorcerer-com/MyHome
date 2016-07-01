#!/usr/bin/env python
import sys, os, threading, subprocess
sys.path.append(os.path.join(os.getcwd(), "External"))
os.chdir("bin")

from External.flask import *
from Page import *
from MyHome import *

logging.getLogger().info("")
app = Flask(__name__)
myHome = MHome()


# autostart: http://blog.scphillips.com/posts/2013/07/getting-a-python-script-to-run-in-the-background-as-a-service-on-boot/
@app.route('/favicon.ico')
def favicon():
	return send_from_directory("..", "MyHome.ico")

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
		
	return template(indexContent(myHome), None) # disable auto refresh for now
	
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
		
	return template(loginContent(invalid))
	
@app.route("/log")
def log():
	if ("password" not in session) or (session["password"] != Config.Password):
		return redirect("/login")
	return template(logContent(), 10) # refresh every 10 seconds
	
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
		myHome.systemChanged = True
		return redirect("/")
		
	return template(settingsContent(system))
	
@app.route("/settings/MediaPlayer", methods=["GET", "POST"])
def mediaPlayer():
	if ("password" not in session) or (session["password"] != Config.Password):
		return redirect("/login")
		
	mediaPlayerSystem = myHome.systems["MediaPlayer"]
	data = request.form if request.method == "POST" else request.args
	if len(data) > 0:
		if "play" in data:
			mediaPlayerSystem.play(data["play"])
		if "action" in data and hasattr(mediaPlayerSystem, data["action"]):
			getattr(mediaPlayerSystem, data["action"])() # call function with set action name
		if "rootPath" in data:
			mediaPlayerSystem.rootPath = str(data["rootPath"])
			myHome.systemChanged = True
		return redirect("/settings/MediaPlayer")
		
	return template(mediaPlayerContent(mediaPlayerSystem), None) # disable auto refresh for now, causing selection problem

@app.route("/settings/Schedule", methods=["GET", "POST"])
def schedule():
	if ("password" not in session) or (session["password"] != Config.Password):
		return redirect("/login")
		
	scheduleSystem = myHome.systems["Schedule"]
	data = request.form if request.method == "POST" else request.args
	if len(data) > 0:
		scheduleSystem.schedule = []
		for arg in data:
			temp = data.getlist(arg)
			for i in range(0, len(temp)):
				if len(scheduleSystem.schedule) <= i:
					scheduleSystem.schedule.append({})
				if arg == "Time":
					value = parse(temp[i], datetime)
				elif arg == "Repeat":
					value = parse(temp[i], timedelta)
				else:
					value = parse(temp[i], str)
				scheduleSystem.schedule[i][arg] = value
		scheduleSystem.schedule.sort(key=lambda x: x["Time"])
		scheduleSystem._nextTime = datetime.now()
		myHome.systemChanged = True
		return redirect("/")
		
	return template(scheduleContent(scheduleSystem))

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