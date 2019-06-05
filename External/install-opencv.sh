#!/usr/bin/env bash
OPENCV_VERSION="3.4.0"

# For some reason I couldn't install libgtk2.0-dev or libgtk-3-dev without running the 
# following line
# See https://www.raspberrypi.org/forums/viewtopic.php?p=1254646#p1254665 for issue and resolution
sudo apt-get install -y devscripts debhelper cmake libldap2-dev libgtkmm-3.0-dev libarchive-dev \
                        libcurl4-openssl-dev intltool
sudo apt-get install -y build-essential cmake pkg-config libjpeg-dev libtiff5-dev libjasper-dev \
                        libpng12-dev libavcodec-dev libavformat-dev libswscale-dev libv4l-dev \
                        libxvidcore-dev libx264-dev libgtk2.0-dev libgtk-3-dev \
                        libatlas-base-dev libblas-dev libeigen3-dev liblapack-dev \
                        gfortran python2.7-dev python3-dev


wget -O "opencv-${OPENCV_VERSION}.tar.gz" "https://github.com/opencv/opencv/archive/${OPENCV_VERSION}.tar.gz"
wget -O "opencv_contrib-${OPENCV_VERSION}.tar.gz" "https://github.com/opencv/opencv_contrib/archive/${OPENCV_VERSION}.tar.gz"
tar -xvf "opencv-${OPENCV_VERSION}.tar.gz"
tar -xvf "opencv_contrib-${OPENCV_VERSION}.tar.gz"

pip3.7 install -U pip
python3.7 -m virtualenv venv
pip install numpy
pip install glibc

cd ./opencv-${OPENCV_VERSION}
mkdir -p build
cd build
cmake -D CMAKE_BUILD_TYPE=RELEASE \
      -D CMAKE_INSTALL_PREFIX=/usr/local \
      -D INSTALL_C_EXMAPLES=ON \
      -D INSTALL_PYTHON_EXAMPLES=ON \
      -D OPENCV_EXTRA_MODULES_PATH="../../opencv_contrib-${OPENCV_VERSION}/modules" \
      -D OPENCV_ENABLE_NONFREE=ON \
      -D BUILD_PERF_TESTS=OFF \
      -D BUILD_TESTS=OFF \
      -D BUILD_DOCS=ON \
      -D BUILD_EXAMPLES=ON \
      -D ENABLE_PRECOMPILED_HEADERS=OFF \
      -D PYTHON3_EXECUTABLE=$(which python3.7) \
      -D BUILD_opencv_python3=yes \
      -D PYTHON3_NUMPY_INCLUDE_DIRS=$(python3.7 -c "import numpy; print(numpy.get_include())") \
      -D PYTHON3_PACKAGES_PATH=$(python3.7 -c "from distutils.sysconfig import get_python_lib; print(get_python_lib())") \
      ../
make -j 4
sudo make install
sudo ldconfig
cd ..
cd ..