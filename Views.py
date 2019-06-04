import logging
import secrets
import time
from datetime import datetime, timedelta

from flask import (Blueprint, Response, abort, redirect, render_template,
                   request, session)
# use to generate hash of the password/token
# from werkzeug.security import generate_password_hash
from werkzeug.security import check_password_hash

from Config import Config
from MyHome import MyHome
from Systems.Sensors.Camera import CameraMovement
from Utils import Utils

Utils.setupLogging(Config.LogFilePath)
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
        if check_password_hash(myHome.config.password, request.form[loginType]):
            logger.info("LogIn: Correct %s", loginType)
            session[loginType] = myHome.config.password
            return redirect("/")
        else:
            logger.warning("LogIn: Invalid %s", loginType)
            invalid = True

    loginType = "token" if "token" in request.args else "password"
    return render_template("login.html", invalid=invalid, loginType=loginType, csrf=session["CSRF"])


@views.route("/")
def index():
    infos = [(system.name, system.isEnabled)
             for system in myHome.systems.values()
             if system.isVisible]
    infos.sort(key=lambda x: x[0])
    infos.append(("Settings", None))
    infos.append(("Logs", None))
    # TODO: show somehow that curtain system has UI, not only enable/disable
    return render_template("index.html", infos=infos)


@views.route("/MediaPlayer", methods=["GET", "POST"])
def MediaPlayer():
    system = myHome.getSystemByClassName("MediaPlayerSystem")
    data = request.form if request.method == "POST" else request.args
    if len(data) > 0:
        if "play" in data:
            system.play(data["play"])
        if "action" in data and hasattr(system, data["action"]):
            # call function with set action name
            getattr(system, data["action"])()
        if "command" in data:
            system.command(data["command"])
        if "refreshSharedList" in data:
            system.refreshSharedList()
        return redirect("/MediaPlayer")

    return render_template("MediaPlayer.html", tree=system.mediaTree, selected=system.playing, watched=system._watched, csrf=session["CSRF"])


@views.route("/Schedule", methods=["GET", "POST"])
def Schedule():
    system = myHome.getSystemByClassName("ScheduleSystem")
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
        return redirect("/")

    items = []
    for item in system._schedule:
        items.append({key: Utils.string(value)
                      for (key, value) in item.items()})
    return render_template("Schedule.html", items=items, enabled=system.isEnabled, csrf=session["CSRF"])


@views.route("/Sensors")
def Sensors():
    system = myHome.getSystemByClassName("SensorsSystem")
    if len(request.args) == 1:
        system.isEnabled = (request.args["enabled"] == "True")
        return redirect("/Sensors")

    now = datetime.now()
    data = {}
    for sensor in system._sensors:
        data[sensor.name] = {"day": {}, "older": {}}  # 1st day data / older
        for subName in sensor.subNames:
            subData = {time: sensor._data[time][subName]
                       for time in sensor._data if subName in sensor._data[time]}
            data[sensor.name]["day"][subName] = {
                time: value for time, value in subData.items() if time >= now - timedelta(days=1)}
            data[sensor.name]["older"][subName] = {time: value for time, value in subData.items() if time < now.replace(
                hour=0, minute=0, second=0, microsecond=0) - timedelta(days=1)}
        data[sensor.name]["metadata"] = sensor.metadata
        data[sensor.name]["token"] = sensor.token
        if sensor.address != "" and not sensor.address.startswith("/") and not sensor.address.startswith("COM"):
            data[sensor.name]["address"] = sensor.address
    if len(system._cameras) > 0:
        data["cameras"] = {
            camera.name: camera.isIPCamera for camera in system._cameras}
    return render_template("Sensors.html", data=data, enabled=system.isEnabled)


@views.route("/cameras/<cameraName>")
def cameras(cameraName):
    system = myHome.getSystemByClassName("SensorsSystem")

    def gen():
        while True:
            imgData = system._camerasDict[cameraName].getImageData()
            yield (b"--frame\r\nContent-Type: image/jpeg\r\n\r\n" + imgData + b"\r\n")
            time.sleep(0.05)  # frame rate : 1 / 0.05 = 20 FPS

    if not "action" in request.args:  # stream
        return Response(gen(), mimetype='multipart/x-mixed-replace; boundary=frame')
    elif request.args["action"] in dir(CameraMovement):  # movement
        system._camerasDict[cameraName].move(
            CameraMovement[request.args["action"]])
        return ""


@views.route("/Security")
def Security():
    system = myHome.getSystemByClassName("SecuritySystem")
    if len(request.args) == 1:
        system.isEnabled = (request.args["enabled"] == "True")
        return redirect("/Security")

    return render_template("Security.html", history=reversed(system._history), enabled=system.isEnabled)


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
            obj = myHome.getSystemByClassName(
                systemName) if systemName != "Config" else myHome.config
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
        return redirect("/")

    return render_template("settings.html", uiManager=myHome.uiManager, csrf=session["CSRF"])


@views.route("/Logs")
def logs():
    return render_template("logs.html", log=reversed(Utils.getLogs()))


@views.route("/restart")
def restart():
    func = request.environ.get('werkzeug.server.shutdown')
    if func is not None:
        func()
    return redirect("/")
