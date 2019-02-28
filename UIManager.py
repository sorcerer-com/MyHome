import logging

from Utils.Decorators import type_check
from Utils import Utils


class UIManager(object):
    """ UI Manager class. """

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the UI Manager class.

        Arguments:
            owner {MyHome} -- MyHome object which is the owner of the config.
        """

        self._owner = owner
        self.containers = {}

    @type_check
    def __repr__(self) -> str:
        """ Return string representation of the object. """

        return "containers[%s]" % len(self.containers)

    @type_check
    def registerContainer(self, obj: object, name: str = None) -> str:
        """ Register object as UI container.

        Arguments:
            obj {None} -- Object to be registered as UI container.

        Keyword Arguments:
            name {str} -- Name of the container. (default: class name)

        Returns:
            UIContainer - Registered UI container.
        """

        name = type(obj).__name__ if name == None else name
        container = UIContainer(self, name)
        items = Utils.getFields(obj)
        for name in items:
            valueType = type(getattr(obj, name))
            displayName = name[0].upper() + name[1:]
            container.properties[name] = UIProperty(
                container, displayName, valueType)
            if valueType is list:
                container.properties[name].subtype = str
            elif valueType is dict:
                container.properties[name].subtype = (str, str)

        self.containers[obj] = container
        return container


class UIContainer(object):
    """ UI Container class. """

    @type_check
    def __init__(self, owner: UIManager, name: str) -> None:
        """ Initialize an instance of the UIContainer class.

        Arguments:
            owner {UIManager} -- UIManager owner of this container.
            name {str} -- Name of the container.
        """

        self.name = name
        self.properties = {}

    @type_check
    def __repr__(self) -> str:
        """ Return string representation of the object. """

        return "%s properties[%s]" % (self.name, len(self.properties))


class UIProperty(object):
    """ UI Property class. """

    @type_check
    def __init__(self, owner: UIContainer, displayName: str, type_: type) -> None:
        """ Initialize an instance of the UIProperty class.

        Arguments:
            owner {UIContainer} -- UIContainer owner of this container.
            displayName {str} -- Display name of the property.
            type_ {type} -- Type of the property.
        """

        self._owner = owner
        self.displayName = displayName
        self.type_ = type_
        self.subtype = None
        self.isPrivate = False

    @type_check
    def __repr__(self) -> str:
        """ Return string representation of the object. """

        result = "%s (%s)" % (self.displayName, self.type_.__name__)
        if self.isPrivate:
            result = "*" + result
        if self.subtype != None:
            result += "[%s]" % str(self.subtype)
        return result
