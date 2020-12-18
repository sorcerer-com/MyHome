import logging
import secrets
import sys
from datetime import datetime, timedelta

from flask import (Blueprint, Response, abort, redirect, render_template,
                   request, session)
# use to generate hash of the password/token
# from werkzeug.security import generate_password_hash
from werkzeug.security import check_password_hash

from Config import Config
from MyHome import MyHome
from Systems.Sensors.Camera import Camera, CameraMovement
from Utils import Utils

debugLevel = logging.INFO if "debug" not in sys.argv else logging.DEBUG
Utils.setupLogging(Config.LogFilePath, debugLevel)
logger = logging.getLogger(__name__.split(".")[-1])

views = Blueprint('views', __name__)
myHome = MyHome()


@views.before_request
def beforeRequest():
    # check CSRF
    if "CSRF" not in session:
        session["CSRF"] = secrets.token_hex(8)
    else:
        if request.method == "POST" and request.form["CSRF"] != session["CSRF"]:
            abort(403)

    isLocalIP = (request.remote_addr == "127.0.0.1")
    for ip in myHome.config.internalIPs:
        isLocalIP |= request.remote_addr.startswith(ip)

    # pass only login
    if ("password" not in session) or (session["password"] != myHome.config.password):
        if request.endpoint == "views.login":
            return None
        elif not isLocalIP:
            abort(404)
        else:
            return redirect("/login")

    # external request
    if not isLocalIP:
        if ("token" not in session) or (session["token"] != myHome.config.token):
            if request.endpoint == "views.login":
                return None
            else:
                return redirect("/login?token")
        else:
            logger.warning("Request: external request to %s from %s",
                           request.endpoint, request.remote_addr)

    session.modified = True
    return None


@views.context_processor
def context_processor():
    return dict(csrf=session["CSRF"], upgradeAvailable=myHome.upgradeAvailable)


@views.route("/upgrade")
def upgrade():
    if request.args["CSRF"] != session["CSRF"]:
        abort(403)
    if myHome.upgrade(False):
        restart()
        return "<META http-equiv=\"refresh\" content=\"15;URL=/\">Upgrade was successfull! Rebooting...\n"
    else:
        myHome.upgradeAvailable = None
    return redirect("/")


@views.route("/robots.txt")
def robots():
    return "User-agent: *\nDisallow: /"


@views.route("/login", methods=["GET", "POST"])
def login():
    if myHome.config.password == "":
        session["password"] = myHome.config.password
        return redirect("/")

    invalid = False
    if request.method == "POST":
        loginType = request.form["loginType"]
        value = myHome.config.password if loginType != "token" else myHome.config.token
        if check_password_hash(value, request.form[loginType]):
            logger.info("LogIn: Correct %s", loginType)
            session[loginType] = value
            return redirect("/")
        else:
            logger.warning("LogIn: Invalid %s", loginType)
            invalid = True

    loginType = "token" if "token" in request.args else "password"
    return render_template("login.html", invalid=invalid, loginType=loginType)


@views.route("/")
def index():
    infos = [(system.name, system.isEnabled)
             for system in myHome.systems.values()
             if system.isVisible]
    infos.sort(key=lambda x: x[0])
    infos.append(("Settings", None))
    infos.append(("Logs", None))
    return render_template("index.html", infos=infos)


@views.route("/AI", methods=["GET", "POST"])
def AI():
    system = myHome.systems["AISystem"]
    if request.method == "GET" and len(request.args) == 1:
        if "enabled" in request.args:
            system.isEnabled = (request.args["enabled"] == "True")
        elif list(request.args.keys())[0] in system._skills:
            system._skills[list(request.args.keys())[0]].isEnabled = (
                list(request.args.values())[0] == "True")
            myHome.systemChanged = True
        return redirect("/AI")
    if request.method == "POST":
        return system.processRecognition(request.form["transcript"].strip().lower(), float(request.form["confidence"]))
    return render_template("AI.html", skills=system._skills, enabled=system.isEnabled)


@views.route("/Drivers", methods=["GET", "POST"])
def Drivers():
    system = myHome.systems["DriversSystem"]
    if request.method == "POST":
        # add new driver
        if request.form["action"] == "new":
            system.addDriver(
                request.form["type"], request.form["name"], request.form["address"])
        # edit driver
        elif request.form["action"] == "edit":
            driver = system._driversDict[request.form["originalName"]]
            for arg in request.form:
                if hasattr(driver, arg):
                    if arg == "name" and request.form[arg] in system._driversDict:
                        continue
                    value = Utils.parse(
                        request.form[arg], type(getattr(driver, arg)))
                    setattr(driver, arg, value)
            myHome.systemChanged = True
        # remove driver
        elif request.form["action"] == "remove":
            driver = system._driversDict[request.form["originalName"]]
            system._drivers.remove(driver)
            myHome.systemChanged = True
        return redirect("/Drivers")

    items = {}
    for driver in system._drivers:
        items[driver.name] = {field: getattr(
            driver, field) for field in Utils.getFields(driver)}
        items[driver.name]["driverType"] = driver.driverType
        items[driver.name]["driverColor"] = driver.driverColor
    return render_template("Drivers.html", items=items, driverTypes=system.driverTypes)


@views.route("/MediaPlayer", methods=["GET", "POST"])
def MediaPlayer():
    system = myHome.systems["MediaPlayerSystem"]
    data = request.form if request.method == "POST" else request.args
    if len(data) > 0:
        if "play" in data:
            system.play(data["play"])
        if "action" in data and hasattr(system, data["action"]):
            # call function with set action name
            getattr(system, data["action"])()
        if "refreshSharedList" in data:
            system.refreshSharedList()
        return redirect("/MediaPlayer")

    volume = 100 + system.volume * 5
    return render_template("MediaPlayer.html", tree=system.mediaTree, selected=system.playing, watched=system._watched, volume=volume, timeDetails=system.timeDetails)


@views.route("/Schedule", methods=["GET", "POST"])
def Schedule():
    system = myHome.systems["ScheduleSystem"]
    if request.method == "GET" and len(request.args) == 1:
        system.isEnabled = (request.args["enabled"] == "True")
        return redirect("/Schedule")
    elif request.method == "POST" and len(request.form) > 0:
        system._schedule = []
        for arg in request.form:
            if arg == "CSRF":
                continue
            values = request.form.getlist(arg)
            for i, value in enumerate(values):
                if len(system._schedule) <= i:
                    system._schedule.append({})
                if arg == "Time":
                    value = Utils.parse(value, datetime)
                elif arg == "Repeat":
                    value = Utils.parse(value, timedelta)
                else:
                    value = value
                system._schedule[i][arg] = value
        system._schedule.sort(key=lambda x: x["Time"])
        system._nextTime = datetime.now()
        myHome.systemChanged = True
        return redirect("/Schedule")

    items = []
    for item in system._schedule:
        items.append({key: Utils.string(value)
                      for (key, value) in item.items()})
    return render_template("Schedule.html", items=items, enabled=system.isEnabled)


@views.route("/Security")
def Security():
    system = myHome.systems["SecuritySystem"]
    if len(request.args) == 1:
        system.isEnabled = (request.args["enabled"] == "True")
        return redirect("/Security")

    return render_template("Security.html", history=reversed(system._history), enabled=system.isEnabled)


@views.route("/Sensors")
def Sensors():
    system = myHome.systems["SensorsSystem"]
    if len(request.args) == 1:
        system.isEnabled = (request.args["enabled"] == "True")
        return redirect("/Sensors")

    now = datetime.now()
    data = {}
    for sensor in system._sensors + system._cameras:
        sensorData = {"day": {}, "older": {}}  # 1st day data / older
        for subName in sensor.subNames:
            subData = {time: sensor._data[time][subName]
                       for time in sensor._data if subName in sensor._data[time]}
            sensorData["day"][subName] = {
                time: value for time, value in subData.items() if time >= now - timedelta(days=1)}
            sensorData["older"][subName] = {time: value for time, value in subData.items() if time < now.replace(
                hour=0, minute=0, second=0, microsecond=0) - timedelta(days=1)}
        sensorData["metadata"] = sensor.metadata
        sensorData["token"] = sensor.token
        if sensor.address != "" and not sensor.address.startswith("/") and not sensor.address.startswith("COM"):
            sensorData["address"] = sensor.address

        if not isinstance(sensor, Camera):
            data[sensor.name] = sensorData
        else:
            if "cameras" not in data:
                data["cameras"] = {}
            sensorData["isIPCamera"] = sensor.isIPCamera
            data["cameras"][sensor.name] = sensorData
    return render_template("Sensors.html", data=data, enabled=system.isEnabled)


@views.route("/cameras/<cameraName>")
def cameras(cameraName):
    system = myHome.systems["SensorsSystem"]
    if cameraName not in system._camerasDict:
        abort(404)

    def gen():
        while True:
            imgData = system._camerasDict[cameraName].getImageData()
            yield (b"--frame\r\nContent-Type: image/jpeg\r\n\r\n" + imgData + b"\r\n")

    if not "action" in request.args:  # stream
        return Response(gen(), mimetype='multipart/x-mixed-replace; boundary=frame')
    elif request.args["action"] in dir(CameraMovement):  # movement
        system._camerasDict[cameraName].move(
            CameraMovement[request.args["action"]])
        return ""


@views.route("/Settings", methods=["GET", "POST"])
def settings():
    if request.method == "POST":
        for arg in request.form:
            if arg == "CSRF":
                continue
            if arg.endswith("[]"):
                value = request.form.getlist(arg)[1:]  # skip first dummy item
                arg = arg[:-2]
                if arg.endswith(":key"):  # dict
                    arg = arg[:-4]
                    temp = request.form.getlist(
                        arg + ":value[]")[1:]  # get values
                    value = {value[i]: temp[i] for i in range(len(value))}
                elif arg.endswith(":value"):  # skip values
                    continue
            else:
                value = str(request.form[arg])

            systemName, prop = arg.split(":")
            obj = myHome.systems[systemName] if systemName != "Config" else myHome.config
            if hasattr(obj, prop):
                uiProperty = myHome.uiManager.containers[obj].properties[prop]
                if isinstance(value, list):
                    value = [Utils.parse(v, uiProperty.subtype) for v in value]
                    setattr(obj, prop, value)
                elif isinstance(value, dict):
                    value = {Utils.parse(k, uiProperty.subtype[0]): Utils.parse(
                        v, uiProperty.subtype[0]) for k, v in value.items()}
                    setattr(obj, prop, value)
                else:
                    setattr(obj, prop, Utils.parse(value, uiProperty.type_))

        myHome.systemChanged = True
        return redirect("/Settings")

    return render_template("settings.html", uiManager=myHome.uiManager)


@views.route("/Logs")
def logs():
    return render_template("logs.html", log=reversed(Utils.getLogs()))


@views.route("/restart")
def restart():
    func = request.environ.get('werkzeug.server.shutdown')
    if func is not None:
        func()
    else:
        import os
        import signal
        from threading import Timer
        myHome.stop()
        # kill after 10 sec
        Timer(10, lambda: os.kill(os.getpid(), signal.SIGILL)).start()
    return redirect("/")
