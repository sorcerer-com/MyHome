import smtplib, poplib
from os.path import basename, isfile
from email.mime.application import MIMEApplication
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText
from email.utils import COMMASPACE, formatdate
from email import parser

import External.mechanize
from Utils.Logger import *
from Utils.Utils import *


class InternetService:
	@staticmethod
	@timeout(10000)
	def sendEMail(send_to, subject, text, files=None):
		Logger.log("info", "Internet Service: send mail to '%s' subject: '%s'" % (str(send_to), subject))
		
		if (not isinstance(send_to, list)) or (len(send_to) == 0) or (send_to[0] == ""):
			Logger.log("error", "Internet Service: cannot send email - invalid email list")
			return False
		
		try:
			msg = MIMEMultipart()
			msg["From"] = Config.EMail
			msg["To"] = COMMASPACE.join(send_to)
			msg["Subject"] = subject
			msg["Date"] = formatdate(localtime=True)
			msg.attach(MIMEText(text))

			for f in files or []:
				if isfile(f):
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
			return True
		except Exception as e:
			Logger.log("error", "Internet Service: cannot send email to '%s' subject: '%s'" % (str(send_to), subject))
			Logger.log("debug", str(e))
			return False
			
	@staticmethod
	@timeout(1000)
	def receiveEMails(send_from = None, subject = None, date = None, maxResult = 10):
		#Logger.log("info", "Internet Service: receive emails")
	
		result = []
		try:
			pop = poplib.POP3_SSL(Config.POP3Server, Config.POP3ServerPort)
			pop.user(Config.EMailUserName)
			pop.pass_(Config.EMailPassword)
			
			msgsCount = len(pop.list()[1])
			for i in reversed(range(1, msgsCount + 1)):
				if len(result) >= maxResult:
					break
					
				msg = "\n".join(pop.retr(i)[1])
				msg = parser.Parser().parsestr(msg)
				if date != None and msg["date"] == date:
					break
				if (send_from == None or send_from in msg["from"]) and \
					(subject == None or subject in msg["subject"]):
					result.append(msg)
				
		except Exception as e:
			Logger.log("error", "Internet Service: cannot receive emails")
			Logger.log("debug", str(e))
			return False
		
		return result
		
	@staticmethod
	def sendSMSByEMail(number, msg):
		Logger.log("info", "Internet Service: send SMS '%s' to %s" % (msg, number))
		if number == "":
			Logger.log("error", "Internet Service: cannot send sms - invalid number")
			return False
			
		return InternetService.sendMail([number + "@sms.telenor.bg"], "", msg)
		
	@staticmethod
	def sendSMS(number, msg, operator):
		Logger.log("info", "Internet Service: send SMS '%s' to %s" % (msg, number))
		if number == "":
			Logger.log("error", "Internet Service: cannot send sms - invalid number")
			return False
			
		try:
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
				return True
				
			else:
				Logger.log("error", "Internet Service: cannot send sms - invalid operator")
				return False
				
		except Exception as e:
			Logger.log("error", "Internet Service: cannot send SMS '%s' to %s" % (msg, number))
			Logger.log("debug", str(e))
			return False