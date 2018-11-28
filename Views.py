import logging
import secrets

from flask import Blueprint, redirect, render_template, request, session, abort
from werkzeug.security import (check_password_hash,
                               generate_password_hash)  # use to generate hash of the password/token

from MyHome import MyHome
from Utils import Utils

logger = logging.getLogger(__name__)

views = Blueprint('views', __name__)


@views.before_request
def beforeRequest():
    # check CSRF
    if "CSRF" not in session:
        session["CSRF"] = secrets.token_hex(8)
    else:
        if request.method == "POST" and request.form["CSRF"] != session["CSRF"]:
            abort(403)

    isLocalIP = (request.remote_addr == "127.0.0.1")
    for ip in MyHome().config.internalIPs:
        isLocalIP |= request.remote_addr.startswith(ip)

    # pass only login
    if ("password" not in session) or (session["password"] != MyHome().config.password):
        if request.endpoint == "views.login":
            return
        elif not isLocalIP:
            abort(404)
        else:
            return redirect("/login")

    # external request
    if not isLocalIP:
        if ("token" not in session) or (session["token"] != MyHome().config.token):
            if request.endpoint == "views.login":
                return
            else:
                return redirect("/login?token")
        else:
            logger.warning("Request: external request to %s from %s" %
                           (request.endpoint, request.remote_addr))

    session.modified = True


@views.route("/login", methods=["GET", "POST"])
def login():
    if MyHome().config.password == "":
        session["password"] = MyHome().config.password
        return redirect("/")

    invalid = False
    if request.method == "POST":
        loginType = request.form["loginType"]
        if check_password_hash(MyHome().config.password, request.form[loginType]):
            logger.info("LogIn: Correct " + loginType)
            session[loginType] = MyHome().config.password
            return redirect("/")
        else:
            logger.warning("LogIn: Invalid" + loginType)
            invalid = True

    loginType = "token" if "token" in request.args else "password"
    return render_template("login.html", invalid=invalid, loginType=loginType, csrf=session["CSRF"])


@views.route("/")
def index():
    infos = [(name, MyHome().systems[name].isEnabled)
             for name in sorted(MyHome().systems.keys())
             if MyHome().systems[name].isVisible]
    infos.append(("Settings", None))
    infos.append(("Logs", None))
    return render_template("index.html", infos=infos)


@views.route("/Logs")
def log():
    return render_template("logs.html", log=reversed(Utils.getLogs()))
