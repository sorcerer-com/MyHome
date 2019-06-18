import logging

from Systems.BaseSystem import BaseSystem
from Utils.Decorators import type_check

logger = logging.getLogger(__name__.split(".")[-1])


class DriversSystem(BaseSystem):
    """ DriversSystem class """

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the DriversSystem class.

        Arguments:
                owner {MyHome} -- MyHome object which is the owner of the system.
        """

        super().__init__(owner)
