import logging
import os
import re
import threading
import warnings

from gtts import gTTS

from Config import Config
from Services import LocalService
from Systems.Skills.BaseSkill import BaseSkill
from Utils.Decorators import type_check

logger = logging.getLogger(__name__.split(".")[-1])


class SpeechSkill(BaseSkill):
    """ SpeechSkill class """

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the SpeechSkill class.

        Arguments:
                owner {AISystem} -- AISystem object which is the owner of the system.
        """

        super().__init__(owner)

        self.startListenKeyword = "Сиси"
        self.stopListenAfter = 60000  # 1 min

    @staticmethod
    @type_check
    def say(text: str) -> None:
        """ Generate and play the speech from the text asynchronous. """

        t = threading.Thread(target=lambda: SpeechSkill._say(text))
        t.daemon = True
        t.start()

    @staticmethod
    @type_check
    def _say(text: str) -> None:
        """ Generate and play the speech from the text. """

        logger.info("Saying: %s", text)

        text = SpeechSkill.symbolsToText(text)
        text = SpeechSkill.digitToText(text)
        text = SpeechSkill.cyrToLat(text)
        with warnings.catch_warnings():
            warnings.simplefilter("ignore")

            tts = gTTS(text=text, lang="cs", lang_check=False)  # pl/cs
            tts.save(Config.BinPath + "say.mp3")

        LocalService.openMedia(os.path.join(
            os.getcwd(), Config.BinPath + "say.mp3"), "local", -300)

        os.remove(Config.BinPath + "say.mp3")

    @staticmethod
    @type_check
    def cyrToLat(text: str) -> str:
        """ Translate the text on cyrillic to latin. """

        symbols = (
            "абвгдезийклмнопрстуфхАБВГДЕЗИЙКЛМНОПРСТУФХ",
            "abwgdeziyklmnoprstufhABWGDEZIYKLMNOPRSTUFH")
        tr = {ord(a): ord(b) for a, b in zip(*symbols)}
        text = text.translate(tr)

        specialChars = {
            "ж": "zh",
            "ц": "c",
            "ч": "ch",
            "ш": "sh",
            "щ": "sht",
            "ъ": "а",
            "ь": "y",
            "ю": "y",
            "я": "ya",
            "Ж": "Zh",
            "Ц": "Ts",
            "Ч": "Ch",
            "Ш": "Sh",
            "Щ": "Sht",
            "Ъ": "A",
            "Ю": "Y",
            "Я": "Ya"}
        for (key, value) in specialChars.items():
            text = text.replace(key, value)
        return text

    @staticmethod
    @type_check
    def digitToText(text: str) -> str:
        """ Convert digits in the text to their text representation. """

        nums = [s for s in re.findall(r"[-+]?\d*\.?\d+", text)]
        for num in sorted(nums, reverse=True):
            s = ""
            for n in num.split('.'):
                if len(s) > 0:
                    s += " цяло и "
                if float(n) < 0:
                    s += "минус "
                absNum = abs(float(n))
                if absNum >= 100:
                    continue
                if absNum >= 20:
                    temp = ["двайсет", "трийсет", "четиресет", "педесет",
                            "шейсет", "седемдесет", "осемдесет", "деведесет"]
                    s += temp[int(absNum / 10) - 2]
                    if (absNum % 10) != 0:
                        s += " и "
                if absNum >= 0 and (absNum == 10 or absNum % 10 != 0):
                    temp = ["едно", "две", "три", "четири", "пет", "шест", "седем", "осем", "девет", "десет",
                            "единайсет", "дванайсет", "тринайсет", "четиринайсет", "петнайсет", "шестнайсет", "седемнайсет", "осемнайсет", "деветнайсет"]
                    if absNum < 20:
                        s += temp[int(absNum % 20) - 1]
                    else:
                        s += temp[int(absNum % 10) - 1]
                if absNum == 0:
                    s += "нула"
            text = text.replace(str(num), s)

        return text

    @staticmethod
    @type_check
    def symbolsToText(text: str) -> str:
        """ Convert special symbols in the text with their text representation. """

        symbols = {"%": " процента", "°": " градуса"}

        for (key, value) in symbols.items():
            text = text.replace(key, value)

        return text
