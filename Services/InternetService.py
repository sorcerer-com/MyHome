import subprocess
import smtplib
from os.path import basename
from email.mime.application import MIMEApplication
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText
from email.utils import COMMASPACE, formatdate

from Utils.Logger import *
import External.mechanize


class InternetService:
	@staticmethod
	def sendEMail(send_to, subject, text, files=None):
		Logger.log("info", "send mail to '" + str(send_to) + "' subject:'" + subject + "'")
		
		assert isinstance(send_to, list)
		msg = MIMEMultipart()
		msg["From"] = Config.EMail
		msg["To"] = COMMASPACE.join(send_to)
		msg["Subject"] = subject
		msg["Date"] = formatdate(localtime=True)
		msg.attach(MIMEText(text))

		for f in files or []:
			with open(f, "rb") as file:
				msg.attach(MIMEApplication(
					file.read(),
					Content_Disposition='attachment; filename="%s"' % basename(f),
					Name=basename(f)
				))
			
		smtp = smtplib.SMTP_SSL(Config.SMTPServer, Config.SMTPServerPort)
		smtp.login(Config.EMailUserName, Config.EMailPassword)
		smtp.sendmail(Config.EMail, send_to, msg.as_string())
		smtp.close()
		
	@staticmethod
	def sendSMSByEMail(number, msg):
		Logger.log("info", "send SMS '" + msg + "' to " + number)
		InternetService.sendMail([number + "@sms.telenor.bg"], "", msg)
		
	@staticmethod
	def sendSMS(number, msg, operator):
		if operator.lower() == "telenor":
			if number.startswith("359"):
				number = "0" + number[3:]
			br = External.mechanize.Browser()
			# login
			br.open("http://my.telenor.bg")
			br.select_form(nr=0)
			br["username"] = number
			br["password"] = Config.MyTelenorPassword
			br.submit()
			# go to sms
			br.follow_link(url_regex="compose")
			# sms
			br.select_form(nr=1)
			br["receiverPhoneNum"] = number
			br["txtareaMessage"] = msg[:99]
			br.submit()
			br.follow_link(url_regex="logout")
			br.close()