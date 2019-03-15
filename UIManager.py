from Utils import Utils
from Utils.Decorators import type_check


class UIManager:
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

        return f"containers[{len(self.containers)}]"

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

        name = type(obj).__name__ if name is None else name
        container = UIContainer(self, name)
        fields = Utils.getFields(obj)
        for field in fields:
            valueType = type(getattr(obj, field))
            displayName = field[0].upper() + field[1:]
            container.properties[field] = UIProperty(
                container, displayName, valueType)
            if valueType is list:
                container.properties[field].subtype = str
            elif valueType is dict:
                container.properties[field].subtype = (str, str)

        self.containers[obj] = container
        return container


class UIContainer:
    """ UI Container class. """

    @type_check
    def __init__(self, owner: UIManager, name: str) -> None:
        """ Initialize an instance of the UIContainer class.

        Arguments:
            owner {UIManager} -- UIManager owner of this container.
            name {str} -- Name of the container.
        """

        self._owner = owner
        self.name = name
        self.properties = {}

    @type_check
    def __repr__(self) -> str:
        """ Return string representation of the object. """

        return f"{self.name} properties[{len(self.properties)}]"


class UIProperty:
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
        self.hint = ""

    @type_check
    def __repr__(self) -> str:
        """ Return string representation of the object. """

        result = f"{self.displayName} ({self.type_.__name__})"
        if self.isPrivate:
            result = "*" + result
        if self.subtype is not None:
            result += f"[{self.subtype}]"
        return result
