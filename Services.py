import logging
import os
import re
import smtplib
import subprocess
import sys
from email.mime.application import MIMEApplication
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText
from email.utils import COMMASPACE, formatdate

import requests
import robobrowser

from Utils.Decorators import try_catch, type_check

logger = logging.getLogger(__name__.split(".")[-1])

# fix crash in robobrowser in br.get_link("logout")
if sys.version_info >= (3, 7):
    # pylint: disable=no-member
    re._pattern_type = re.Pattern


class LocalService:
    """ Provides local functionalities. """

    @staticmethod
    @try_catch("Cannot open media", None)
    @type_check
    def openMedia(path: str, audioOutput: str = "hdmi", volume: int = 0, wait: bool = True) -> object:
        """ Open media file.

        Arguments:
            path {str} -- Path to the media file.

        Keyword Arguments:
            audioOutput {str} -- Type of the audio output (local or hdmi) (default: {"hdmi"})
            volume {int} -- Value of the volume. (default: {0})
            wait {bool} -- True if should wait playback to finish. (default: {True})

        Returns:
            object -- Return code of the process (when wait) or Popen object.
        """

        logger.info("Open media '%s'", path)
        if path == "" or audioOutput not in ["hdmi", "local"]:
            logger.error("Cannot open media - invalid parameters")
            return None

        call = subprocess.call if wait else subprocess.Popen
        return call(["omxplayer", "-r", "-o", audioOutput, "--vol", str(volume), path], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, close_fds=True, encoding="utf-8")


class InternetService:
    """ Provides internet functionalities. """

    @staticmethod
    @try_catch("Cannot send email", False)
    @type_check
    def sendEMail(smtp_server_info: dict, send_from: str, send_to: list, subject: str, text: str, files: list = None) -> bool:
        """ Send email.

        Arguments:
            smtp_server_info {dict} -- Info of the SMTP server (address, port, usernam, password)
            send_from {str} -- Email address of the sender.
            send_to {list} -- Email address of the receiver.
            subject {str} -- Subject of the email.
            text {str} -- Content of the email.

        Keyword Arguments:
            files {list} -- List with files paths to be attached to the email. (default: {None})

        Returns:
            bool -- True if the email was sent successfully, otherwise false.
        """

        logger.info("Send mail to '%s' subject: '%s'", str(send_to), subject)

        if len(send_to) == 0 or send_to[0] == "":
            logger.error("Cannot send email - invalid email list")
            return False

        msg = MIMEMultipart()
        msg["From"] = send_from
        msg["To"] = COMMASPACE.join(send_to)
        msg["Subject"] = subject
        msg["Date"] = formatdate(localtime=True)
        msg.attach(MIMEText(text))

        for f in files or []:
            if os.path.isfile(f):
                with open(f, "rb") as file:
                    msg.attach(MIMEApplication(
                        file.read(),
                        Content_Disposition=f"attachment; filename='{os.path.basename(f)}'",
                        Name=os.path.basename(f)
                    ))
            else:
                logger.warning("Invalid email file attachment - %s", f)

        smtp = smtplib.SMTP_SSL(smtp_server_info["address"], int(
            smtp_server_info["port"]), timeout=60)
        smtp.login(smtp_server_info["username"], smtp_server_info["password"])
        smtp.sendmail(send_from, send_to, msg.as_string())
        smtp.close()
        return True

    @staticmethod
    @try_catch("Cannot send sms", False)
    @type_check
    def sendSMS(number: str, operator: str, password: str, msg: str) -> bool:
        """ Send sms to the set phone number.

        Arguments:
            number {str} -- Phone number.
            operator {str} -- Name of the mobile operator.
            password {str} -- Password for the mobile operator portal.
            msg {str} -- Content of the message.

        Returns:
            bool -- True if the sms was sent successfully, otherwise false.
        """

        logger.info("Send SMS '%s' to %s", msg.replace("\n", " "), number)
        if number == "":
            logger.error("Cannot send sms - invalid number")
            return False

        if operator.lower() == "telenor":
            br = robobrowser.RoboBrowser(parser="html.parser")
            # login
            br.open("http://my.telenor.bg")
            form = br.get_form()
            form["account"] = number[1:]  # +359number
            br.submit_form(form)
            form = br.get_form()
            form["pin"] = password
            br.submit_form(form)
            # go to sms
            br.follow_link(br.get_link(href="compose"))
            # sms
            form = br.get_forms()[1]
            form["receiverPhoneNum"] = number
            form["txtareaMessage"] = msg[:99]
            br.submit_form(form)
            br.follow_link(br.get_link("logout"))
            return True

        logger.error("Cannot send sms - invalid operator")
        return False

    @staticmethod
    @try_catch("Cannot get json content", None)
    @type_check
    def getJsonContent(url: str) -> object:
        """ Gets JSON content after a request to the set URL.

        Arguments:
            url {str} -- URL which will respond with JSON.

        Returns:
            str -- JSON content of the set URL.
        """

        logger.debug("Get json content from '%s'", url)
        if url == "":
            logger.error("Cannot get json content - invalid url")
            return None

        r = requests.get(url, timeout=5)
        if r.status_code == requests.codes.ok:
            return r.json()
        return None

    @staticmethod
    @try_catch("Cannot get weather", [None, None])
    @type_check
    def getWeather() -> list:
        """ Weather info about current and next day.

        Returns:
            list -- The weather info about current and next day.
        """

        def getNumberOnly(text: str) -> object:
            temp = "".join(
                [s for s in text if s.isdigit() or s == '.' or s == '-'])
            return float(temp) if temp.find('.') > -1 else int(temp)

        def getDayWeather(elem: object) -> dict:
            result = {}
            temp = elem.find("span", class_="wfNonCurrentTemp")
            result["minTemp"] = getNumberOnly(temp.next.strip().lower())
            temp = elem.find("span", class_="wfNonCurrentTemp").find("span")
            result["maxTemp"] = getNumberOnly(temp.next.strip().lower())
            temp = elem.find("strong", class_="wfNonCurrentCond")
            result["condition"] = temp.next.strip().lower()
            temp = elem.find("span", class_="wfNonCurrentBottom")
            result["wind"] = temp.contents[1].attrs["title"].strip().lower()
            result["rainProb"] = getNumberOnly(
                temp.contents[3].next.strip().lower())
            result["rainAmount"] = getNumberOnly(
                temp.contents[5].next.strip().lower())
            result["stormProb"] = getNumberOnly(
                temp.contents[7].next.strip().lower())
            result["cloudiness"] = getNumberOnly(
                temp.contents[9].next.strip().lower())
            return result

        br = robobrowser.RoboBrowser(parser="html.parser")
        br.open("https://www.sinoptik.bg/sofia-bulgaria-100727011")
        result = []
        result.append(getDayWeather(
            br.find("a", class_="wfTodayContent wfNonCurrentContent")))
        temp = br.find("span", class_="wfCurrentTemp")
        result[0]["curTemp"] = getNumberOnly(temp.next.strip().lower())
        result.append(getDayWeather(
            br.find("a", class_="wfTomorrowContent wfNonCurrentContent")))
        return result
