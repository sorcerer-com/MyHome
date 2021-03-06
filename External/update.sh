printf "\nRaspberry update\n"
rpi-update

printf "\nApplication update\n"
apt-get update

printf "\nApplication upgrade\n"
apt-get dist-upgrade

printf "\nApplication auto-remove\n"
apt-get autoremove

printf "\nApplication clean\n"
apt-get clean

read -p "Do you want to restart?" yn
if [ "$yn" = "Y" ] || [ "$yn" = "y" ] ; then
	reboot
fi

