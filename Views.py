import logging
import secrets

from flask import Blueprint, abort, redirect, render_template, request, session
from werkzeug.security import \
    generate_password_hash  # use to generate hash of the password/token
from werkzeug.security import check_password_hash

from MyHome import MyHome
from Utils import Utils

logger = logging.getLogger(__name__)

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
            return
        elif not isLocalIP:
            abort(404)
        else:
            return redirect("/login")

    # external request
    if not isLocalIP:
        if ("token" not in session) or (session["token"] != myHome.config.token):
            if request.endpoint == "views.login":
                return
            else:
                return redirect("/login?token")
        else:
            logger.warning("Request: external request to %s from %s" %
                           (request.endpoint, request.remote_addr))

    session.modified = True


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
            logger.info("LogIn: Correct " + loginType)
            session[loginType] = myHome.config.password
            return redirect("/")
        else:
            logger.warning("LogIn: Invalid " + loginType)
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
    return render_template("index.html", infos=infos)


@views.route("/Settings", methods=["GET", "POST"])
def settings():
    if request.method == "POST":
        for arg in request.form:
            if arg == "CSRF":
                continue
            if arg.endswith("[]"):
                value = request.form.getlist(arg)
                arg = arg[:-2]
            else:
                value = str(request.form[arg])

            systemName, prop = arg.split(":")
            obj = myHome.getSystemByClassName(
                systemName) if systemName != "Config" else myHome.config
            if hasattr(obj, prop):
                uiProperty = myHome.uiManager.containers[obj].properties[prop]
                if type(value) is list:
                    value = [Utils.parse(v, uiProperty.subtype) for v in value]
                    setattr(obj, prop, value)
                else:
                    setattr(obj, prop, Utils.parse(value, uiProperty.type_))

        myHome.systemChanged = True
        return redirect("/")

    return render_template("settings.html", uiManager=myHome.uiManager, csrf=session["CSRF"])

@views.route("/MediaPlayer", methods=["GET", "POST"])
def MediaPlayer():
    system = myHome.getSystemByClassName("MediaPlayerSystem")
    data = request.form if request.method == "POST" else request.args
    if len(data) > 0:
        if "play" in data:
            system.play(data["play"])
        if "action" in data and hasattr(system, data["action"]):
            getattr(system, data["action"])() # call function with set action name
        if "command" in data:
            system.command(data["command"])
        if "refreshSharedList" in data:
            system.refreshSharedList()
        return redirect("/MediaPlayer")
        
    return render_template("MediaPlayer.html", tree=system.mediaTree, selected=system.playing, watched=system._watched, csrf=session["CSRF"])


@views.route("/restart")
def restart():
    func = request.environ.get('werkzeug.server.shutdown')
    if func is not None:
        func()
    return redirect("/")


@views.route("/Logs")
def logs():
    return render_template("logs.html", log=reversed(Utils.getLogs()))
