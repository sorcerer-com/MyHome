import sys
import time
import cv2
import numpy as np
import base64
from datetime import datetime, timedelta
import traceback

capture = cv2.VideoCapture()
lastUse = datetime.now() + timedelta(minutes=-1)


def get_capture(address):
    global capture, lastUse
    if not capture.isOpened() and datetime.now() - lastUse > timedelta(minutes=1):
        capture.open(int(address) if address.isnumeric() else address)
        if capture.isOpened():
            capture.read()  # dump one image to prepare the capture

        lastUse = datetime.now()

    if capture.isOpened():
        lastUse = datetime.now()

    return capture


def get_image(address, initIfEmpty=True, size=None, timestamp=True):
    global lastUse
    capture = get_capture(address)
    result, img = capture.read()
    if img is None:  # add empty image with red X
        if capture.isOpened():
            capture.release()  # try to release the camera and open it again next time
        lastUse = datetime.now() - timedelta(minutes=1)

        if not initIfEmpty:
            return result, None
        img = np.zeros((480, 640, 3), np.uint8)
        cv2.line(img, (0, 0), (640, 480), (0, 0, 255), 2, cv2.LINE_AA)
        cv2.line(img, (640, 0), (0, 480), (0, 0, 255), 2, cv2.LINE_AA)
    else:
        result = True
    if size is not None:
        img = cv2.resize(img, size)
    if timestamp:
        scale = img.shape[0] / 800  # height / 800
        text = time.strftime("%d/%m/%Y %H:%M:%S")
        textSize, _ = cv2.getTextSize(
            text, cv2.FONT_HERSHEY_SIMPLEX, scale, round(scale * 3))
        cv2.putText(img, text, (5, 5 + textSize[1]), cv2.FONT_HERSHEY_SIMPLEX,
                    scale, (255, 255, 255), round(scale * 3), cv2.FILLED)
    return result, img


def drop_old_frames(address):
    capture = get_capture(address)
    # if capture is not open or we use it less than 1 seconds ago
    if not capture.isOpened():
        print(0)
        return

    timeout_timer = datetime.now()
    timer = datetime.now()
    # drop frames until it took less than 100 ms for a frame (not buffered) or timeout - 3 sec
    i = 0
    while datetime.now() - timer < timedelta(milliseconds=100) and datetime.now() - timeout_timer < timedelta(seconds=3):
        timer = datetime.now()
        res, _ = capture.read()
        i += 1
        if not res:
            break
    return i


def diff_images(img1, img2):
    img1 = cv2.resize(img1, (640, 480))
    img1 = cv2.cvtColor(img1, cv2.COLOR_BGR2GRAY)
    img1 = cv2.GaussianBlur(img1, (21, 21), 0)

    img2 = cv2.resize(img2, (640, 480))
    img2 = cv2.cvtColor(img2, cv2.COLOR_BGR2GRAY)
    img2 = cv2.GaussianBlur(img2, (21, 21), 0)

    diff = cv2.absdiff(img1, img2)
    _, diff = cv2.threshold(diff, 25, 255, cv2.THRESH_BINARY)
    return cv2.countNonZero(diff) / (640 * 480)


if __name__ == "__main__":
    try:
        for line in sys.stdin:
            try:
                if line.startswith("isOpened"):
                    args = line.split()  # isOpened <address>
                    print(get_capture(args[1]).isOpened())

                if line.startswith("getImage"):
                    # getImage <address> [initIfEmpty(True/False)] [width,height] [timestamp(True/False)]
                    args = line.split()
                    initIfEmpty = args[2].lower() == "true" if len(args) > 2 else True
                    size = (int(args[3].split(",")[0]), int(args[3].split(",")[1])) if len(args) > 3 and args[3] != "None" else None
                    timestamp = args[4].lower() == "true" if len(args) > 4 else True
                    res, img = get_image(args[1], initIfEmpty, size, timestamp)
                    print(res)
                    print(base64.b64encode(cv2.imencode(".jpg", img)[1]))

                if line.startswith("dropOldFrames"):
                    args = line.split()  # dropOldFrames <address>
                    res = drop_old_frames(args[1])
                    print(res)

                if line.strip() == "diffImages": # diffImages \n <img1> \n <img2>
                    img1 = base64.b64decode(sys.stdin.readline())
                    np_img1 = np.frombuffer(img1, dtype=np.uint8)
                    img2 = base64.b64decode(sys.stdin.readline())
                    np_img2 = np.frombuffer(img1, dtype=np.uint8)
                    diff = diff_images(cv2.imdecode(np_img1, flags=1),
                                       cv2.imdecode(np_img2, flags=1))
                    print(diff)
                    
                if line.strip() == "exit":
                    break

                sys.stdout.flush()
            except Exception:
                sys.stderr.write(f"Failed to execute camera command {line}:\n{traceback.format_exc()}\n")
    finally:
        capture.release()
        sys.stderr.write("Close camera\n")
