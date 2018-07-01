#!/usr/bin/env python
import sys, os, threading, subprocess
sys.path.insert(0, os.path.join(os.getcwd(), "External"))
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
	
	if ("password" not in session) or (session["password"] != Config.Password): # pass only login
		if request.endpoint == "login":
			return
		elif not isLocalIP and request.endpoint != "index":
			abort(404)
		else:
			return redirect("/login")
		
	if not isLocalIP:
		if request.endpoint == "login" or ("token" in session and session["token"] == Config.Token):
			return
		elif request.endpoint != "cameras" and request.endpoint != "camerasImage":
			infos = [(name, myHome.systems[name].enabled) for name in sorted(myHome.systems.keys())]
			content = "<h2 class='title'>External Request</h2>\n"
			for info in infos:
				content += "%s %s<br/>\n" % (info[0], info[1])
			return render_template("base.html", content=content)
		else:
			Logger.log("warning", "Request: external request to cameras from " + request.remote_addr)
	
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
	invalid = False
	token = "token" in data and "password" in session and session["password"] == Config.Password
	if "password" in data: 
		if str(hash(data["password"])) == Config.Password:
			Logger.log("info", "LogIn: correct password")
			session["password"] = str(hash(data["password"]))
			return redirect("/")
		else:
			Logger.log("warning", "LogIn: invalid password")
			invalid = True
	elif token:
		if str(hash(data["token"])) == Config.Token:
			Logger.log("info", "LogIn: correct token")
			session["token"] = str(hash(data["token"]))
			return redirect("/")
		elif str(data["token"]) != "":
			Logger.log("warning", "LogIn: invalid token")
			invalid = True
	
	return render_template("login.html", invalid=invalid, token=token)
		
@app.route("/cameras")
def cameras():
	sensorsSystem = myHome.systems[SensorsSystem.Name]
	return render_template("cameras.html", camerasCount=sensorsSystem.camerasCount, sensorsData=sensorsSystem.getLatestData())

@app.route("/cameras/<cameraIndex>")
def camerasImage(cameraIndex):
	def gen():
		import StringIO
		sensorsSystem = myHome.systems[SensorsSystem.Name]
		while True:
			img = sensorsSystem.getImage(int(cameraIndex), (320, 240))
			if img == None:
				time.sleep(1)
				continue
			frame = StringIO.StringIO()
			img.save(frame, "jpeg")
			yield (b"--frame\r\nContent-Type: image/jpeg\r\n\r\n" + frame.getvalue() + b"\r\n")
			time.sleep(0.1)
			
	return Response(gen(), mimetype='multipart/x-mixed-replace; boundary=frame')


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
		if "command" in data:
			mediaPlayerSystem.command(data["command"])
		if "refreshSharedList" in data:
			mediaPlayerSystem.refreshSharedList()
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
		
	# {sensorName: [{subname: {time: value}}, {subname: {time: value}}, {subname: {time: value}}] # 3 list elements - 1st day, 5 days, older
	data = collections.OrderedDict()
	for sensor in sensorsSystem._sensors.values():
		data[sensor.Name] = [collections.OrderedDict(), collections.OrderedDict(), collections.OrderedDict()]
		for subName in sensor.subNames:
			data[sensor.Name][0][subName] = sensor.getData(subName, datetime.now() - timedelta(days=1), datetime.now())
			data[sensor.Name][1][subName] = sensor.getData(subName, datetime.now() - timedelta(days=6), datetime.now() - timedelta(days=1))
			data[sensor.Name][2][subName] = sensor.getData(subName, datetime.now() - timedelta(days=366), datetime.now() - timedelta(days=6))
	# (day,night,total,price)
	powerConsumption = sensorsSystem.getMonthlyPowerConsumption()
	return render_template("Sensors.html", data=data, powerConsumption=powerConsumption, enabled=sensorsSystem.enabled)

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
			
			if hasattr(type(system), prop):
				attrType = type(getattr(type(system), prop))
				if attrType is not property:
					setattr(type(system), prop, parse(value, attrType))
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
	app.config['TEMPLATES_AUTO_RELOAD'] = True
	app.run(debug=False, host="0.0.0.0", threaded=True)

if __name__ == "__main__":
	if len(sys.argv) > 1 and sys.argv[1] == "-service":
		start()
	else:		
		t = threading.Thread(target=start)
		t.daemon = True
		t.start()

		try:
			subprocess.call(["sensible-browser", "localhost:5000"], stdout=subprocess.PIPE, stderr=subprocess.PIPE, close_fds=True)
		except:
			t.join()
			
	myHome.__del__()