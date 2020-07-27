import logging
import re
import time
from datetime import datetime, timedelta
from enum import Enum
from threading import Lock

import cv2
import numpy as np
# https://github.com/FalkTannhaeuser/python-onvif-zeep
from onvif import ONVIFCamera

from Utils.Decorators import try_catch, type_check

logger = logging.getLogger(__name__.split(".")[-1])


class CameraMovement(Enum):
    UP = 1
    DOWN = 2
    RIGHT = 3
    LEFT = 4
    ZOOMIN = 5
    ZOOMOUT = 6


class Camera:
    """ Camera class """

    @type_check
    def __init__(self, name: str, address: str) -> None:
        """ Initialize an instance of the Camera class.

        Arguments:
            name {str} -- Name of the camera.
            address {str} -- (IP) Address / local number of the camera.
        """

        self.name = name
        self.address = address

        self._capture = None
        self._lastUse = datetime.now()

        self._onvif = None
        self._mutex = Lock()

    @property
    @type_check
    def isIPCamera(self) -> bool:
        """ Gets whether current camera instance is a IP Camera. """

        return not self.address.isdigit()

    @property
    @type_check
    def capture(self) -> object:
        """ Get an instance of the cv2.VideoCapture. """

        if self._capture is None:
            logger.info("Opening camera: %s", self.name)
            self._capture = cv2.VideoCapture(self._getRealAddress())
        # if capture isn't opened try again but only if the previous try was at least 1 minutes ago
        elif not self._capture.isOpened() and datetime.now() - self._lastUse > timedelta(minutes=1):
            logger.info("Opening camera: %s", self.name)
            self._capture.open(self._getRealAddress())
            self._lastUse = datetime.now()
        if self._capture.isOpened():
            self._lastUse = datetime.now()
        return self._capture

    @type_check
    def update(self) -> None:
        """ Update current camera's state. """

        # release the capture if it isn't used for more then 5 minutes
        if self._capture is not None and self._capture.isOpened() and datetime.now() - self._lastUse > timedelta(minutes=5):
            logger.info("Release camera: %s", self.name)
            self._capture.release()

    @type_check
    def getImage(self, size: tuple = None, timeStamp: bool = True) -> object:
        """ Get current image from the camera.

        Keyword Arguments:
            size {tuple} -- Size of the image, if None - default camera size. (default: {None})
            timeStamp {bool} -- If true time stamp will be added to the image. (default: {True})

        Returns:
            object -- OpenCV image.
        """

        with self._mutex:
            img = self.capture.read()[1]
        if img is None:  # add empty image with red X
            self._capture.release() # try to release the camera and open it again next time
            self._lastUse = datetime.now() - timedelta(minutes=1)
            img = np.zeros((480, 640, 3), np.uint8)
            cv2.line(img, (0, 0), (640, 480), (0, 0, 255), 2, cv2.LINE_AA)
            cv2.line(img, (640, 0), (0, 480), (0, 0, 255), 2, cv2.LINE_AA)
        if size is not None:
            img = cv2.resize(img, size)
        if timeStamp:
            scale = img.shape[0] / 800  # height / 800
            text = time.strftime("%d/%m/%Y %H:%M:%S")
            textSize, _ = cv2.getTextSize(
                text, cv2.FONT_HERSHEY_SIMPLEX, scale, round(scale * 3))
            cv2.putText(
                img, text, (5, 5 + textSize[1]), cv2.FONT_HERSHEY_SIMPLEX, scale, (255, 255, 255), round(scale * 3), cv2.LINE_AA)
        return img

    @type_check
    def getImageData(self, imgFormat: str = "jpg", size: tuple = None, timeStamp: bool = True) -> str:
        """ Get current image data from the camera.

        Keyword Arguments:
            imgFormat {str} -- Image format to be used. (default: {"jpg"})
            size {tuple} -- Size of the image, if None - default camera size. (default: {None})
            timeStamp {bool} -- If true time stamp will be added to the image. (default: {True})

        Returns:
            str -- Binary representation of the image in the set format.
        """

        img = self.getImage(size, timeStamp)
        return cv2.imencode(f".{imgFormat}", img)[1].tostring()

    @type_check
    def saveImage(self, filename: str, size: tuple = None, timeStamp: bool = True) -> None:
        """ Save image to the set filename.

        Arguments:
            filename {str} -- Filename of the saved image.

        Keyword Arguments:
            size {tuple} -- Size of the image, if None - default camera size. (default: {None})
            timeStamp {bool} -- If true time stamp will be added to the image. (default: {True})
        """

        logger.debug("Camera '%s' save image: %s", self.name, filename)

        img = self.getImage(size, timeStamp)
        cv2.imwrite(filename, img)

    @try_catch("Cannot move camera")
    @type_check
    def move(self, movement: CameraMovement) -> None:
        """ Move camera in the set direction.

        Arguments:
            movement {CameraMovement} -- Movement direction.
        """

        logger.debug("Camera '%s' move: %s", self.name, movement)

        if not self.isIPCamera:
            return

        self._setupOnvifCamera()
        if self._onvif is None:
            return

        self._onvif["Moverequest"].Velocity.PanTilt.x = 0
        self._onvif["Moverequest"].Velocity.PanTilt.y = 0
        self._onvif["Moverequest"].Velocity.Zoom.x = 0
        if movement == CameraMovement.UP:
            self._onvif["Moverequest"].Velocity.PanTilt.y = self._onvif[
                "PTZConfigurationOptions"].Spaces.ContinuousPanTiltVelocitySpace[0].YRange.Max
        elif movement == CameraMovement.DOWN:
            self._onvif["Moverequest"].Velocity.PanTilt.y = self._onvif[
                "PTZConfigurationOptions"].Spaces.ContinuousPanTiltVelocitySpace[0].YRange.Min
        elif movement == CameraMovement.LEFT:
            self._onvif["Moverequest"].Velocity.PanTilt.x = self._onvif[
                "PTZConfigurationOptions"].Spaces.ContinuousPanTiltVelocitySpace[0].XRange.Max
        elif movement == CameraMovement.RIGHT:
            self._onvif["Moverequest"].Velocity.PanTilt.x = self._onvif[
                "PTZConfigurationOptions"].Spaces.ContinuousPanTiltVelocitySpace[0].XRange.Min
        elif movement == CameraMovement.ZOOMIN:
            self._onvif["Moverequest"].Velocity.Zoom.x = self._onvif[
                "PTZConfigurationOptions"].Spaces.ContinuousZoomVelocitySpace[0].XRange.Max
        elif movement == CameraMovement.ZOOMOUT:
            self._onvif["Moverequest"].Velocity.Zoom.x = self._onvif[
                "PTZConfigurationOptions"].Spaces.ContinuousZoomVelocitySpace[0].XRange.Min
        self._onvif["PTZ"].ContinuousMove(self._onvif["Moverequest"])
        time.sleep(1)  # Move for 1 second then stop
        self._onvif["PTZ"].Stop(
            {'ProfileToken': self._onvif["Moverequest"].ProfileToken, "PanTilt": True, "Zoom": True})

    @try_catch("Cannot get real address", "")
    @type_check
    def _getRealAddress(self) -> object:
        """ Return real address - device id, rtsp url or rtsp url from ONVIF. """

        if not self.isIPCamera:
            return int(self.address)

        # rtsp://192.168.0.120:554/user=admin_password=12345_channel=1_stream=0.sdp?real_stream
        if self.address.startswith("rtsp://"):
            return self.address

        self._setupOnvifCamera()
        request = {'ProfileToken': self._onvif["MediaProfile"].token, 'StreamSetup': {
            'Stream': 'RTP-Unicast', 'Transport': 'RTSP'}}
        response = self._onvif["Media"].GetStreamUri(request)
        responseIp = response.Uri[7:response.Uri.find(":", 7)]
        # username:password@ip:port
        _, _, ip, _ = re.split(':|@', self.address)
        return response.Uri.replace(responseIp, ip)

    @type_check
    def _setupOnvifCamera(self) -> None:
        """ Setup ONVIF Camera environment. """

        logger.debug("Camera '%s' setup onvif", self.name)

        if not self.isIPCamera or self._onvif is not None:
            return

        try:
            self._onvif = {}
            username, password, ip, port = re.split(
                ':|@', self.address)  # username:password@ip:port #8899
            self._onvif["Camera"] = ONVIFCamera(ip, port, username, password)

            self._onvif["Media"] = self._onvif["Camera"].create_media_service()
            self._onvif["PTZ"] = self._onvif["Camera"].create_ptz_service()

            self._onvif["MediaProfile"] = self._onvif["Media"].GetProfiles()[0]

            request = self._onvif["PTZ"].create_type('GetConfigurationOptions')
            request.ConfigurationToken = self._onvif["MediaProfile"].PTZConfiguration.token
            self._onvif["PTZConfigurationOptions"] = self._onvif["PTZ"].GetConfigurationOptions(
                request)

            self._onvif["Moverequest"] = self._onvif["PTZ"].create_type(
                'ContinuousMove')
            self._onvif["Moverequest"].ProfileToken = self._onvif["MediaProfile"].token
            if self._onvif["Moverequest"].Velocity is None:
                self._onvif["Moverequest"].Velocity = self._onvif["PTZ"].GetStatus(
                    {'ProfileToken': self._onvif["MediaProfile"].token}).Position
        except Exception:
            logger.exception("Cannot setup ONVIF camera")
            self._onvif = None
