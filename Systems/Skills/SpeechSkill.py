import logging

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
