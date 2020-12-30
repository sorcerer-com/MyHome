import logging
import pkgutil
from configparser import RawConfigParser

from Utils import Utils
from Utils.Decorators import type_check
from Utils.TaskManager import TaskManager

from Systems.BaseSystem import BaseSystem
from Systems.Skills.AssistenSkill import AssistenSkill
from Systems.Skills.BaseSkill import BaseSkill
from Systems.Skills.SpeechSkill import SpeechSkill

logger = logging.getLogger(__name__.split(".")[-1])

# import all skills
for _, modname, _ in pkgutil.walk_packages(["./Systems/Skills"], "Systems.Skills."):
    __import__(modname)


class AISystem(BaseSystem):
    """ AISystem class """

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the AISystem class.

        Arguments:
                owner {MyHome} -- MyHome object which is the owner of the system.
        """

        super().__init__(owner)

        # skills
        self._skills = {}
        for cls in BaseSkill.__subclasses__():
            self._skills[cls.__name__] = cls(self)

    @type_check
    def load(self, configParser: RawConfigParser, data: dict) -> None:
        """ Loads settings and data for the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser from which the settings will be loaded.
                data {dict} -- Dictionary from which the system data will be loaded.
        """

        super().load(configParser, data)

        keys = sorted(self._skills.keys())
        for key in keys:
            if key in self._skills:
                skillData = {}
                if self._skills[key].name in data:
                    skillData = Utils.deserializable(
                        data[self._skills[key].name])
                self._skills[key].load(skillData)

    @type_check
    def save(self, configParser: RawConfigParser, data: dict) -> None:
        """ Saves settings and data used by the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser to which the settings will be saved.
                data {dict} -- Dictionary to which the system data will be saved.
        """

        super().save(configParser, data)

        keys = sorted(self._skills.keys())
        for key in keys:
            skillData = {}
            self._skills[key].save(skillData)
            data[key] = Utils.serializable(skillData)

    @type_check
    def update(self) -> None:
        """ Update current system's state. """

        super().update()

        # update skills
        for skill in self._skills.values():
            if skill.isEnabled:
                TaskManager().execute(skill, skill.update)

    @type_check
    def __dir__(self) -> list:
        """ Return all attributes.

        Returns:
            list -- List of all attributes.
        """

        result = super().__dir__()
        for skill in self._skills.values():
            items = Utils.getFields(skill)
            for name in items:
                result.append(skill.name + "." + name)
        return sorted(set(result))

    @type_check
    def __getattr__(self, attr: str) -> object:
        """ If attribute is part of the skill return its value.

        Arguments:
            attr {str} -- Attribute to get its value.

        Raises:
            AttributeError: If attribute isn't part of the skill.

        Returns:
            object -- Value of the attribute.
        """

        if "." in attr and hasattr(self, "_skills"):
            for skill in self._skills.values():
                items = Utils.getFields(skill)
                for name in items:
                    if skill.name + "." + name == attr:
                        return getattr(skill, name)
        raise AttributeError("%r object has no attribute %r" %
                             (self.__class__.__name__, attr))

    @type_check
    def __setattr__(self, attr: str, value: object) -> None:
        """ Set attribute value.

        Arguments:
            attr {str} -- Attribute name.
            value {object} -- Value to be set.
        """

        if "." in attr and hasattr(self, "_skills"):
            for skill in self._skills.values():
                items = Utils.getFields(skill)
                for name in items:
                    if skill.name + "." + name == attr:
                        setattr(skill, name, value)
                        return
        super().__setattr__(attr, value)

    @type_check
    def processRecognition(self, transcript: str, confidence: float) -> None:
        """ Process speech recognition.

        Arguments:
            transcript {str} -- Transcription of the speech.
            confidence {float} -- Confidence of the transcription (between 0 and 1)
        """

        transcript = transcript.replace(
            self._skills["SpeechSkill"].startListenKeyword.lower(), "").strip()
        response = self._skills[AssistenSkill.__name__].processVoiceCommand(
            transcript, confidence)
        SpeechSkill.say(response)
        return response
