import codecs
import logging
import os
import socket
from configparser import RawConfigParser

from nmb.NetBIOS import NetBIOS
from smb.SMBConnection import SMBConnection

from Services import LocalService
from Systems.BaseSystem import BaseSystem
from Utils.Decorators import try_catch, type_check

logger = logging.getLogger(__name__.split(".")[-1])


class MediaPlayerSystem(BaseSystem):
    """ MediaPlayerSystem class """
    SupportedFormats = [".mkv", ".avi", ".mov", ".wmv",
                        ".mp4", ".mpg", ".mpeg", ".m4v", ".3gp", ".mp3"]

    @type_check
    def __init__(self, owner: None) -> None:
        """ Initialize an instance of the MediaPlayerSystem class.

        Arguments:
                owner {MyHome} -- MyHome object which is the owner of the system.
        """

        super().__init__(owner)
        self._isEnabled = None

        self.mediaPath = "."
        self.sharedPath = ""
        self.volume = 0
        self.radios = []

        self._sharedList = []
        self._playing = ""
        self._watched = []
        self._process = None

    @type_check
    def load(self, configParser: RawConfigParser, data: dict) -> None:
        """ Loads settings and data for the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser from which the settings will be loaded.
                data {dict} -- Dictionary from which the system data will be loaded.
        """

        super().load(configParser, data)

        if "sharedList" in data:
            self._sharedList = data["sharedList"]
        if "watched" in data:
            self._watched = data["watched"]

    @type_check
    def save(self, configParser: RawConfigParser, data: dict) -> None:
        """ Saves settings and data used by the system.

        Arguments:
                configParser {RawConfigParser} -- ConfigParser to which the settings will be saved.
                data {dict} -- Dictionary to which the system data will be saved.
        """

        super().save(configParser, data)

        data["sharedList"] = self._sharedList
        data["watched"] = self._watched

    @property
    @type_check
    def mediaList(self) -> list:
        """ Gets a list with all playable media.

        Returns:
            list -- List of media files.
        """

        result = []
        if os.path.isdir(self.mediaPath):
            for root, subFolders, files in os.walk(self.mediaPath):
                subFolders.sort()
                files.sort()
                for f in files:
                    if os.path.splitext(f)[1] in self.SupportedFormats:
                        path = os.path.join(root, f)
                        result.append(
                            "local\\" + os.path.relpath(path, self.mediaPath))
        # shared
        result.extend(["shared\\" + i for i in self._sharedList])
        # add radios
        result.extend(["radios\\" + i for i in self.radios])
        return result

    @property
    @type_check
    def mediaTree(self) -> dict:
        """ Gets a tree view with all playable media.

        Returns:
            dict -- Dictionary with all playable media.
        """

        tree = {}
        for f in self.mediaList:
            split = f.split("\\")
            currItem = tree
            for s in split:
                if s not in currItem:
                    currItem[s] = {}
                currItem = currItem[s]
        return tree

    @property
    @type_check
    def playing(self) -> str:
        """ Gets the playing file.

        Returns:
            str -- The playing file.
        """

        if (self._process is None) or (self._process.poll() is not None):
            self._playing = ""
            self._process = None
        return self._playing

    @type_check
    def play(self, path: str) -> None:
        """ Play the set media file.

        Arguments:
            path {str} -- Media file to be played.
        """

        logger.debug("Play media: %s", path)

        if path == "":
            return

        self._playing = path
        _type = path[:path.index("\\")]
        path = path[path.index("\\") + 1:]  # remove local/shared/radios prefix
        if _type == "radios":
            self._process = LocalService.openMedia(
                path, "local", int(self.volume*300*1.5), False)
        else:
            if _type == "local":
                path = os.path.join(self.mediaPath, path)
                self._convertSubtitles(path)
            else:
                path = self.sharedPath + path
            self._process = LocalService.openMedia(
                path, volume=self.volume*300, wait=False)
        if (self._process is not None) and (self._process.poll() is None):
            if _type != "radios":
                self._markAsWatched(self._playing)
            self._owner.event(self, "MediaPlayed", self._playing)

    @type_check
    def stop(self) -> None:
        """ Stop the current playing. """

        logger.debug("Stop media: %s", self._playing)
        self._playing = ""
        if (self._process is not None) and (self._process.poll() is None):
            self._process.stdin.write("q")
            self._process.stdin.flush()
            self._owner.event(self, "MediaStopped")

    @type_check
    def pause(self) -> None:
        """ Pause the current playing. """

        logger.debug("Pause media: %s", self._playing)
        if (self._process is not None) and (self._process.poll() is None):
            self._process.stdin.write(" ")  # space
            self._process.stdin.flush()
            self._owner.event(self, "MediaPaused")

    @type_check
    def volumeDown(self) -> None:
        """ Volume down the current playing. """

        logger.debug("Volume down media: %s", self.volume)
        if (self._process is not None) and (self._process.poll() is None):
            self._process.stdin.write("-")
            self._process.stdin.flush()
            self.volume -= 1
            self._owner.systemChanged = True
            self._owner.event(self, "MediaVolumeDown")

    @type_check
    def volumeUp(self) -> None:
        """ Volume up the current playing. """

        logger.debug("Volume up media: %s", self.volume)
        if (self._process is not None) and (self._process.poll() is None):
            self._process.stdin.write("+")
            self._process.stdin.flush()
            self.volume += 1
            self._owner.systemChanged = True
            self._owner.event(self, "MediaVolumeUp")

    @type_check
    def seekBack(self) -> None:
        """ Seek back the current playing. """

        logger.debug("Seek back media")
        if (self._process is not None) and (self._process.poll() is None):
            self._process.stdin.write("\027[D")  # left arrow
            self._process.stdin.flush()
            self._owner.event(self, "MediaSeekBack")

    @type_check
    def seekForward(self) -> None:
        """ Seek forward the current playing. """

        logger.debug("Seek forward media")
        if (self._process is not None) and (self._process.poll() is None):
            self._process.stdin.write("\027[C")  # right arrow
            self._process.stdin.flush()
            self._owner.event(self, "MediaSeekForward")

    @type_check
    def seekBackFast(self) -> None:
        """ Seek back fast the current playing. """

        logger.debug("Seek back fast media")
        if (self._process is not None) and (self._process.poll() is None):
            self._process.stdin.write("\027[B")  # down arrow
            self._process.stdin.flush()
            self._owner.event(self, "MediaSeekBackFast")

    @type_check
    def seekForwardFast(self) -> None:
        """ Seek forward fast the current playing. """

        logger.debug("Seek forward fast media")
        if (self._process is not None) and (self._process.poll() is None):
            self._process.stdin.write("\027[A")  # up arrow
            self._process.stdin.flush()
            self._owner.event(self, "MediaSeekForwardFast")

    @type_check
    def command(self, cmd: str) -> None:
        """ Send command to the media player. """

        logger.debug("Send command: %s", cmd)
        if (self._process is not None) and (self._process.poll() is None):
            self._process.stdin.write(cmd)
            self._process.stdin.flush()
            self._owner.event(self, "MediaCommand", cmd)

    @type_check
    def refreshSharedList(self) -> None:
        """ Refresh the list with shared files. """

        logger.debug("Refresh shared list")
        self._sharedList = self._listShared()
        self._owner.systemChanged = True

    @type_check
    def _markAsWatched(self, path: str) -> None:
        """ Mark the set media path as watched.

        Arguments:
            path {str} -- Path to be set as watched.
        """

        logger.debug("Mark as watched: %s", path)
        # cleanup watched list from nonexistent files
        self._watched = [w for w in self._watched if w in self.mediaList]

        if path not in self._watched:
            self._watched.append(path)
        self._owner.systemChanged = True

    @try_catch("Cannot list shared folder", [])
    @type_check
    def _listShared(self) -> list:
        """ List media files in a shared location.

        Returns:
            list -- List of media files.
        """

        path = self.sharedPath
        # path format should be: smb://username:password@host_ip/folder/
        if not path.startswith("smb://"):
            return []
        path = path[6:]

        if ":" not in path or "@" not in path:
            return []

        username = path.split(":")[0]
        path = path[len(username)+1:]

        password = path.split("@")[0]
        path = path[len(password)+1:]

        server_ip = path.split("/")[0]
        path = path[len(server_ip)+1:]

        basepath = path.split("/")[0]
        path = path[len(basepath):]

        hostname = socket.gethostname()
        if hostname:
            hostname = hostname.split('.')[0]
        else:
            hostname = f"SMB{os.getpid()}"

        netBios = NetBIOS()
        server_name = netBios.queryIPForName(server_ip)[0]
        netBios.close()

        conn = SMBConnection(username, password, hostname,
                             server_name, use_ntlm_v2=True)
        assert conn.connect(server_ip)

        result = []
        subPaths = [path]  # recursion list
        while len(subPaths) > 0:
            for file in conn.listPath(basepath, subPaths[0]):
                if file.isDirectory and not file.filename.startswith("."):
                    subPaths.append(os.path.join(subPaths[0], file.filename))
                elif os.path.splitext(file.filename)[1] in self.SupportedFormats:
                    fullpath = self.sharedPath.strip(
                        "/") + os.path.join(subPaths[0], file.filename)
                    result.append(os.path.relpath(fullpath, self.sharedPath))
            subPaths.remove(subPaths[0])
        conn.close()
        return result

    @try_catch("Cannot convert subtitles")
    @type_check
    def _convertSubtitles(self, path: str) -> None:
        """ Convert subtitles to utf-8 encoding.

        Arguments:
            path {str} -- Path to media file.
        """

        root = os.path.splitext(path)[0]
        # convert encoding to utf8 without BOM
        if os.path.isfile(root + ".srt"):
            logger.debug("Convert subtitles to utf-8: %s.srt", root)
            try:
                with codecs.open(root + ".srt", "r", encoding="windows-1251") as f1:
                    content = f1.read()
                with codecs.open(root + ".srt", "w", encoding="utf8") as f2:
                    f2.write(content)
            except UnicodeDecodeError:
                pass
