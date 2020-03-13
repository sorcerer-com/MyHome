import logging
import random
import re
from datetime import datetime, timedelta

from Systems.Skills.BaseSkill import BaseSkill
from Utils.Decorators import try_catch, type_check

logger = logging.getLogger(__name__.split(".")[-1])


class AssistenSkill(BaseSkill):
    """ AssistenSkill class """

    _HelloResponses = ["Да?", "Какво обичате?", "Как да ви помогна?"]
    _LowConfidenceResponses = ["Моля?", "Не ви чух", "Може ли да повторите?"]
    _UnknownVoiceCommandResponses = [
        "Не ви разбирам!", "Не знам какво значи това!", "Какво значи това!"]

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the AssistenSkill class.

        Arguments:
                owner {AISystem} -- AISystem object which is the owner of the system.
        """

        super().__init__(owner)

        self._owner._owner.event += self._onEventReceived

        self._commands = []
        self._voiceCommands = {}
        self._commandHistory = []

        # current time
        self._commands.append(
            ["from datetime import datetime", "result = '%s и %s' % (datetime.now().hour % 12, datetime.now().minute)"])
        # weather
        self._commands.append(
            ["from Services import InternetService", "weather = InternetService.getWeather()[0]", "{3}"])
        self._commands.append(
            ["from Services import InternetService", "weather = InternetService.getWeather()[1]", "{3}"])
        self._commands.append(
            ["if weather is None: ",
             "  result = 'В момента не мога да кажа какво ще е времето'",
             "else:",
             "	result  = 'Времето ще е %s. ' % weather['condition'].replace(', ', ' с ')",
             "	if 'curTemp' in weather: result += 'В момента е %s градуса, максималната ще е %s. ' % (weather['minTemp'], weather['maxTemp'])",
             "	else: result += 'Минималната температура ще е %s градуса, максималната %s. ' % (weather['minTemp'], weather['maxTemp'])",
             "	result += 'Вятарът ще е %s от %s. ' % (weather['wind'].split(', ')[1], weather['wind'].split(', ')[0])",
             "	if weather['rainProb'] > 30: result += 'Има %s процента вероятност за дъжд с интензитет %s мм. ' % (weather['rainProb'], weather['rainAmount'])",
             "	if weather['stormProb'] > 30: result += 'Вероятността за буря е %s процента. ' % weather['stormProb']",
             "	result += 'Oблачността ще е %s процента. ' % weather['cloudiness']"])
        # math
        self._commands.append(
            ["a1 = AssistenSkill.textToDigit(args[0])",
             "a2 = AssistenSkill.textToDigit(args[1])"])
        self._commands.append(["{4}", "result = str(a1 + a2)"])
        self._commands.append(["{4}", "result = str(a1 - a2)"])
        self._commands.append(["{4}", "result = str(a1 * a2)"])
        self._commands.append(["{4}", "result = str(a1 / a2)"])
        # inner temperature
        self._commands.append(
            ["for value in myHome.systems['SensorsSystem'].latestData.values():",
             "  if 'Temperature' in value:",
             "    result = str(value['Temperature']) + ' градуса'",
             "    break"])
        # reminder
        self._commands.append(
            ["from datetime import datetime, timedelta",
             "value = AssistenSkill.textToDigit(args[0])",
             "now = datetime.now()"])
        self._commands.append(
            ["command = 'from Systems.Skills.SpeechSkill import SpeechSkill\\n"
                "SpeechSkill.say(\"Напомням ви да ' + args[2] + '\")'",
             "item = {'Name': args[2], 'Time': time, 'Repeat': repeat, "
                "'Color': 'rgba(128, 128, 128, 0.3)', 'Command': command}",
             "myHome.systems['ScheduleSystem']._schedule.append(item)",
             "myHome.systems['ScheduleSystem']._schedule.sort(key=lambda x: x['Time'])",
             "myHome.systems['ScheduleSystem']._nextTime = now",
             "myHome.systemChanged = True",
             "result = 'Разбира се'"])
        self._commands.append(
            ["{10}",
             "time = now + AssistenSkill.textToInterval(args[1], value)",
             "repeat = timedelta()",
             "{11}"])
        self._commands.append(
            ["{10}",
             "repeat = AssistenSkill.textToInterval(args[1], value)",
             "time = now + repeat",
             "{11}"])

        self._voiceCommands["колко е часа"] = 0
        self._voiceCommands["какво ще е времето днес"] = 1
        self._voiceCommands["какво ще е времето утре"] = 2
        self._voiceCommands["колко е {} плюс {}"] = 5
        self._voiceCommands["колко е {} минус {}"] = 6
        self._voiceCommands["колко е {} по {}"] = 7
        self._voiceCommands["колко е {} делено на {}"] = 8
        self._voiceCommands["колко градуса е тук"] = 9
        # ... след x минута/и / час/а / ден/дни / седмица/и / месец/а
        self._voiceCommands["напомни ми след {} {} да {}"] = 12
        self._voiceCommands["напомни ми през {} {} да {}"] = 13

    @type_check
    def _onEventReceived(self, sender: object, event: str, data: object = None) -> None:
        """ Event handler.

        Arguments:
                sender {object} -- Sender of the event.
                event {str} -- Event type.
                data {object} -- Data associated with the event.
        """

        # TODO: on security alarm activated...

        # TODO: remove
        if sender == self._owner and event == "IsEnabledChanged":
            pass
        pass

    @type_check
    def processVoiceCommand(self, transcript: str, confidence: float) -> str:
        """ Process voice command and return response.

        Arguments:
            transcript {str} -- Transcription of the speech.
            confidence {float} -- Confidence of the transcription (between 0 and 1)

        Returns:
            str -- Response of the voice command.
        """

        logger.info("Processing voice command: %s(%s)", transcript, confidence)

        # too low confidence
        if confidence < 0.5:
            return random.choice(AssistenSkill._LowConfidenceResponses)

        # hello response
        if transcript == "":
            return random.choice(AssistenSkill._HelloResponses)

        result = ""
        command, params = self._getCommand(transcript)
        if command is None:
            if len(self._commandHistory) > 0 and \
                self._commandHistory[-1][1] in AssistenSkill._UnknownVoiceCommandResponses and \
                    transcript.startswith("това е като "):
                vCommand = transcript[len("това е като "):]
                commandIdx, _ = self._getCommandInfo(vCommand)
                if commandIdx is not None:  # found explanation
                    self._voiceCommands[self._commandHistory[-1]
                                        [0]] = commandIdx
                    self._owner.systemChanged = True
                    result = "Разбирам"
                else:
                    result = "И това не знам какво е"

            if result == "":
                result = random.choice(
                    AssistenSkill._UnknownVoiceCommandResponses)
        else:
            result = self._executeCommand(command, params)

        self._commandHistory.append((transcript, result))
        # keep last 10 entries
        self._commandHistory = self._commandHistory[-10:]
        return result

    @type_check
    def _getCommandInfo(self, voiceCommand: str) -> tuple:
        """ Get command idx based on the set voice command.

        Arguments:
            voiceCommand {str} -- The voice command.

        Returns:
            tuple -- Tuple with index of the command or None if not found and list with command's parameters
        """

        if voiceCommand in self._voiceCommands:
            return self._voiceCommands[voiceCommand], ()
        else:  # match patterns
            for vCommand in self._voiceCommands:
                if "{" not in vCommand:
                    continue
                reg = re.sub(r"\{\}", r"(\\S*)", vCommand)
                # if it ends with arg, take everything even with whitespace
                if reg.endswith("(\\S*)"):
                    reg = reg[:-1] + ".*" + reg[-1:]
                match = re.match(reg, voiceCommand)
                if match:
                    return self._voiceCommands[vCommand], match.groups()
        return None, ()

    @type_check
    def _getCommand(self, voiceCommand: str) -> tuple:
        """ Get the command associated with the set voice command.

        Arguments:
            voiceCommand {str} -- Voice command.

        Returns:
            tuple -- Command that is associated with the set voice command and its params.
        """

        commandIdx, commandParams = self._getCommandInfo(voiceCommand)
        if commandIdx is None:
            return None, commandParams

        command = self._commands[commandIdx]
        i = 0
        while i < len(command):
            if command[i].startswith("{") and command[i].endswith("}"):
                idx = int(command[i][1:-1])
                command = command[:i] + self._commands[idx] + command[i+1:]
            else:
                i += 1
        return "\n".join(command), commandParams

    @try_catch("Cannot execute command", "Не мога да изпълня командата")
    @type_check
    def _executeCommand(self, command: str, params: tuple) -> str:
        """ Execute command.

        Arguments:
            command {str} -- Command to be executed.
            params {tuple} -- Command's parameters.

        Returns:
            str -- Response of the command.
        """

        # pylint: disable=exec-used
        logger.debug("AI System: execute command '%s'",
                     command.replace("\n", "\\n"))
        self._owner._owner.event(self, "CommandExecuted", command)

        _locals = {"myHome": self._owner._owner, "args": params, "result": ""}
        exec(command, globals(), _locals)
        return _locals["result"]

    @staticmethod
    @type_check
    def textToDigit(text: str) -> str:
        """ Convert the text representation of the digit to text. """

        if text.isdigit():
            return int(text)

        text = text.lower()
        if text == "нула":
            return 0
        if text in ("една", "един"):
            return 1
        if text == "два":
            return 2

        temp = ["едно", "две", "три", "четири", "пет", "шест", "седем", "осем", "девет", "десет",
                "единайсет", "дванайсет", "тринайсет", "четиринайсет", "петнайсет", "шестнайсет", "седемнайсет", "осемнайсет", "деветнайсет"]
        if text in temp:
            return temp.index(text) + 1

        temp2 = ["двайсет", "трийсет", "четиресет", "педесет",
                 "шейсет", "седемдесет", "осемдесет", "деведесет"]
        if text in temp2:
            return (temp2.index(text) + 2) * 10

        for t2 in temp2:
            for i, t in enumerate(temp):
                if text == t2 + " и " + t:
                    return (temp2.index(t2) + 2) * 10 + (i + 1)

        return text

    @staticmethod
    @type_check
    def textToInterval(text: str, value: int) -> str:
        """ Convert the text representation of the interval to text. """

        if text in ('минута', 'минути'):
            interval = timedelta(minutes=value)
        elif text in ('час', 'часa'):
            interval = timedelta(hours=value)
        elif text in ('ден', 'дена', 'дни'):
            interval = timedelta(days=value)
        elif text in ('седмица', 'седмици'):
            interval = timedelta(weeks=value)
        elif text in ('месец', 'месеца'):
            now = datetime.now()
            newMonth = now.month + value
            interval = now.replace(year=now.year+newMonth/12,
                                   month=newMonth % 12) - now
        return interval
