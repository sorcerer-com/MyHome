import logging

from Utils.Decorators import type_check

logger = logging.getLogger(__name__.split(".")[-1])


class BaseSkill:
    """ BaseSkill class """

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the BaseSkill class.

        Arguments:
                owner {AISystem} -- AISystem object which is the owner of the system.
        """

        self._owner = owner
        self.isEnabled = True

    @type_check
    def load(self, _: dict) -> None:
        """ Loads skill's data.

        Arguments:
                data {dict} -- Dictionary from which the skill data will be loaded.
        """

        logger.debug("Load skill: %s", self.name)

    @type_check
    def save(self, _: dict) -> None:
        """ Saves skill's data.

        Arguments:
                data {dict} -- Dictionary to which the skill data will be saved.
        """

        logger.debug("Save skill: %s", self.name)

    @type_check
    def update(self) -> None:
        """ Update current skill's state. """
        pass

    @property
    @type_check
    def name(self) -> str:
        """ Gets name of the skill.

        Returns:
            str -- Name of the skill.
        """

        return self.__class__.__name__
