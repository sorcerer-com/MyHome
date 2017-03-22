#!/usr/bin/env python
import sys, os, threading, subprocess
sys.path.append(os.path.join(os.getcwd(), "External"))
os.chdir("bin")

from External.flask import *
from MyHome import *

logging.getLogger().info("")
template_dir = os.path.abspath("../Pages")
app = Flask(__name__, template_folder=template_dir)
myHome = MHome()


# autostart: http://blog.scphillips.com/posts/2013/07/getting-a-python-script-to-run-in-the-background-as-a-service-on-boot/

@app.before_request
def beforeRequest():
	if request.endpoint == "robots" or request.endpoint == "favicon" or request.endpoint == "images" or \
		request.endpoint == "style" or request.endpoint == "scripts":
		return
		
	isLocalIP = request.remote_addr == "127.0.0.1";
	for ip in Config.InternalIPs:
		isLocalIP |= request.remote_addr.startswith(ip)
	if not isLocalIP and request.endpoint != "favicon":
		if ("password" not in session) or (session["password"] != Config.Password):
			Logger.log("warning", "Request: external request from " + request.remote_addr)
			return login()
		elif request.endpoint != "cameras" and request.endpoint != "camerasImage":
			return render_template("base.html", content="<h2 class='title'>External Request</h2>\n")
		else:
			Logger.log("warning", "Request: external request to cameras from " + request.remote_addr)
	
	if ("password" not in session) or (session["password"] != Config.Password):
		if request.endpoint == "cameras" or request.endpoint == "camerasImage":
			abort(404)
		if request.endpoint != "login":
			return redirect("/login")
	
	session.modified = True
	
@app.route("/robots.txt")
def robots():
	return "User-agent: *\nDisallow: /";

@app.route("/favicon.ico")
def favicon():
	return send_from_directory(os.path.join(template_dir, "Images"), "MyHome.ico")


@app.route("/Images/<image>")
def images(image):
	return send_from_directory(os.path.join(template_dir, "Images"), image)

@app.route("/style.css")
def style():
	return send_from_directory(template_dir, "style.css")

@app.route("/scripts.js")
def scripts():
	return send_from_directory(template_dir, "scripts.js")

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
		
	return render_template("login.html", invalid=invalid)
		
@app.route("/cameras", methods=["GET"])
def cameras():
	system = myHome.systems[SensorsSystem.Name]
	for i in range(0, system.camerasCount):
		img = system.getImage(i, (320, 240))
		if img == None:
			continue
		img.save("camera%d.jpg" % i)
	
	data = request.form if request.method == "POST" else request.args
	autorefresh = True
	if "autorefresh" in data:
		autorefresh = data["autorefresh"] == "True"
	return render_template("cameras.html", time=datetime.now(), camerasCount=system.camerasCount, sensorsData=system.getLatestData(), autorefresh=autorefresh)

@app.route("/cameras/<cameraName>", methods=["GET"])
def camerasImage(cameraName):
	return send_from_directory(".", cameraName)


@app.route("/")
def index():
	infos = [(name, myHome.systems[name].enabled) for name in sorted(myHome.systems.keys())]
	infos.append(("Settings", None))
	infos.append(("Log", None))
	return render_template("index.html", infos=infos)

@app.route("/<systemName>")
def system(systemName):
	if systemName in myHome.systems:
		myHome.systems[systemName].enabled = not myHome.systems[systemName].enabled
	return redirect("/")
	
@app.route("/MediaPlayer", methods=["GET", "POST"])
def MediaPlayer():
	mediaPlayerSystem = myHome.systems[MediaPlayerSystem.Name]
	data = request.form if request.method == "POST" else request.args
	if len(data) > 0:
		if "play" in data:
			mediaPlayerSystem.play(data["play"])
		if "action" in data and hasattr(mediaPlayerSystem, data["action"]):
			getattr(mediaPlayerSystem, data["action"])() # call function with set action name
		if "volume" in data:
			mediaPlayerSystem.volume = int(data["volume"])
			myHome.systemChanged = True
		if "rootPath" in data:
			mediaPlayerSystem.rootPath = str(data["rootPath"])
			myHome.systemChanged = True
		return redirect("/MediaPlayer")
	
	return render_template("MediaPlayer.html", list=mediaPlayerSystem._list, selected=mediaPlayerSystem.getPlaying())

@app.route("/Schedule", methods=["GET", "POST"])
def Schedule():
	scheduleSystem = myHome.systems[ScheduleSystem.Name]
	data = request.form if request.method == "POST" else request.args
	if len(data) == 1:
		value = data["enabled"] == "True"
		scheduleSystem.enabled = value
		return redirect("/Schedule")
	elif len(data) > 1:
		scheduleSystem._schedule = []
		for arg in data:
			temp = data.getlist(arg)
			for i in range(0, len(temp)):
				if len(scheduleSystem._schedule) <= i:
					scheduleSystem._schedule.append({})
				if arg == "Time":
					value = parse(temp[i], datetime)
				elif arg == "Repeat":
					value = parse(temp[i], timedelta)
				else:
					value = parse(temp[i], str)
				scheduleSystem._schedule[i][arg] = value
		scheduleSystem._schedule.sort(key=lambda x: x["Time"])
		scheduleSystem._nextTime = datetime.now()
		myHome.systemChanged = True
		return redirect("/")
		
	items = []
	for item in scheduleSystem._schedule:
		items.append({key: string(value) for (key, value) in item.items()})
	return render_template("Schedule.html", items=items, enabled=scheduleSystem.enabled)

@app.route("/Sensors")
def Sensors():
	sensorsSystem = myHome.systems[SensorsSystem.Name]
	data = request.form if request.method == "POST" else request.args
	if len(data) == 1:
		value = data["enabled"] == "True"
		sensorsSystem.enabled = value
		return redirect("/Sensors")

	data1 = [[key for key in sorted(sensorsSystem._data.keys()) if None not in sensorsSystem._data[key] and (datetime.now() - key).days < 1]]
	data2 = [[key for key in sorted(sensorsSystem._data.keys()) if None not in sensorsSystem._data[key] and (datetime.now() - key).days >= 1 and (datetime.now() - key).days <= 5]]
	data3 = [[key for key in sorted(sensorsSystem._data.keys()) if None not in sensorsSystem._data[key] and (datetime.now() - key).days > 5]]
	for i in range(0, len(sensorsSystem.sensorTypes)):
		data1.append([sensorsSystem._data[key][i] for key in data1[0]])
		data2.append([sensorsSystem._data[key][i] for key in data2[0]])
		data3.append([sensorsSystem._data[key][i] for key in data3[0]])
	return render_template("Sensors.html", types = sensorsSystem.sensorTypes, data1=data1, data2=data2, data3=data3, enabled=sensorsSystem.enabled)

@app.route("/AI", methods=["GET", "POST"])
def AI():
	aiSystem = myHome.systems[AISystem.Name]
	data = request.form if request.method == "POST" else request.args
	if len(data) == 1:
		value = data["enabled"] == "True"
		aiSystem.enabled = value
		return redirect("/AI")
	if len(data) > 1:
		return aiSystem.processVoiceCommand(data["transcript"].strip().lower(), float(data["confidence"]))
		
	return render_template("AI.html", enabled=aiSystem.enabled)
	
@app.route("/Settings", methods=["GET", "POST"])
def settings():
	data = request.form if request.method == "POST" else request.args
	if len(data) > 0:
		for arg in data:
			if arg.endswith("[]"):
				value = str(data.getlist(arg))
				arg = arg[:-2]
			else:
				value = str(data[arg])
			
			systemName, prop = arg.split(":")
			systemName = systemName.replace(" System", "")
			system = myHome.systems[systemName] if "Config" not in systemName else Config
			
			if hasattr(system, prop):
				attrType = type(getattr(system, prop))
				setattr(system, prop, parse(value, attrType))
		myHome.systemChanged = True
		return redirect("/")

	items = {}
	for name, system in myHome.systems.iteritems():
		items[name + " System"] = {prop: getattr(system, prop) for prop in getProperties(system)}
	items[" Config "] = {prop: getattr(Config, prop) for prop in Config.list()}
	# to string values
	for name, props in items.iteritems():
		for key, value in props.iteritems():
			if type(value) is not list:
				items[name][key] = string(value)
			else:
				items[name][key] = [string(v) for v in value]
	return render_template("settings.html", items=items)

@app.route("/Log")
def log():
	return render_template("log.html", log=reversed(Logger.data))
	
@app.route("/test")
def test():
	myHome.sendAlert("Test")
	return redirect("/")
	
@app.route("/restart")
def restart():
	func = request.environ.get('werkzeug.server.shutdown')
	if func is not None:
		func()
	return redirect("/")


def start():
	app.secret_key = u"\xf2N\x8a 8\xb1\xd9(&\xa6\x90\x12R\xf0\\\xe8\x1e\xf92\xa6AN\xed\xb3"
	app.permanent_session_lifetime = timedelta(minutes=15)
	app.run(debug=False, host="0.0.0.0")

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