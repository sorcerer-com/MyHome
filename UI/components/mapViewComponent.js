setComponent("map-view", {
    props: ["rooms"],
    data: function () {
        return {
            mapMissing: false,
            dragMapFile: false,
            mapRatio: 1,
            touches: [],
            edit: false,
            selected: null,

            showDevice: null
        }
    },
    methods: {
        dateToString: dateToString,
        dragOverMapFile: function (e) {
            e.preventDefault();
            this.dragMapFile = this.mapMissing || this.edit;
        },
        uploadMapFile: function (e) {
            e.preventDefault();
            if ((!this.mapMissing && !this.edit) || e.dataTransfer.files.length == 0)
                return;

            this.dragMapFile = false;
            let formData = new FormData();
            formData.append("file", e.dataTransfer.files[0]);
            setMap(formData).done(() => {
                this.mapMissing = false;
            });
        },

        deviceIcon: function (device) {
            if (device.$type.endsWith("Camera"))
                return "videocam";
            else if (device.$baseTypes.some(t => t.endsWith("Sensor")) && device.Grouped)
                return "sensors";
            else if (device.$baseTypes.some(t => t.endsWith("ISwitchDriver")) &&
                !device.$baseTypes.some(t => t.endsWith('ILightDriver')) &&
                !device.$type.endsWith('EwelinkRfDriver')) {
                return device.IsOn ? "toggle_on" : "toggle_off";
            }
            else if (device.$baseTypes.some(t => t.endsWith("ILightDriver")))
                return device.IsOn ? "lightbulb" : "lightbulb_outline";
            else if (device.$baseTypes.some(t => t.endsWith("IAcDriver")))
                return "air";
            else if (device.$type.endsWith('EwelinkRfDriver'))
                return "settings_remote";
            else if (device.$baseTypes.some(t => t.endsWith("ISpeakerDriver")))
                return null; // "speaker";
            else if (device.$baseTypes.some(t => t.endsWith("IMediaDriver")))
                return null; // "play_circle";
            else if (device.$type.endsWith('ScriptDriver'))
                return "code";
        },


        editMap: function () {
            this.edit = !this.edit;
            if (this.edit)
                this.resetMap();
        },
        touch: function (e) {
            if (e.type == "touchstart") {
                this.touches = e.touches || [];
                this.selected = null;
            }

            if (e.touches.length == 1 && this.touches.length == 1) {
                e.buttons = 1;
                e.movementX = e.touches[0].pageX - this.touches[0].pageX;
                e.movementY = e.touches[0].pageY - this.touches[0].pageY;
                e.pageX = e.touches[0].pageX;
                e.pageY = e.touches[0].pageY;

                this.move(e);
            } else if (e.touches.length == 2 && this.touches.length == 2) {
                let dOld = Math.hypot(this.touches[0].pageX - this.touches[1].pageX,
                    this.touches[0].pageY - this.touches[1].pageY)
                let dNew = Math.hypot(e.touches[0].pageX - e.touches[1].pageX,
                    e.touches[0].pageY - e.touches[1].pageY);
                e.deltaY = -(dNew - dOld) * 3;

                this.scaleMap(e);
            }
            this.touches = e.touches || [];
        },
        move: function (e) {
            if (this.edit)
                this.moveDevice(e);
            else if (!this.mapMissing)
                this.moveMap(e);
        },
        
        resetMap: function () {
            let map = $("#mapImage");
            // scale to fit
            let rw = map.parent().width() / map.width();
            let rh = map.parent().height() / map.height();
            let ratio = rw < rh ? rw : rh;
            if (isFinite(ratio)) {
                this.mapRatio *= ratio * 0.9;
                map.width(map.width() * ratio * 0.9); // height is auto resized to keep the aspect ratio
            }

            // center
            map.css({
                position: "absolute",
                left: (map.parent().width() - map.width()) / 2,
                top: (map.parent().height() - map.height()) / 2
            });

            this.stickDevices(map);
        },
        zoomRoom: function (roomName) {
            if (this.edit)
                return;

            let room = this.rooms.find(r => r.Name == roomName);
            if (room == null)
                return;
                
            let minLocation = { x: room.Devices[0]?.Location.X || 0, y: room.Devices[0]?.Location.Y || 0 };
            let maxLocation = { x: 0, y: 0 };
            for (let device of room.Devices) {
                minLocation.x = Math.min(minLocation.x, device.Location.X);
                minLocation.y = Math.min(minLocation.y, device.Location.Y);
                maxLocation.x = Math.max(maxLocation.x, device.Location.X);
                maxLocation.y = Math.max(maxLocation.y, device.Location.Y);
            }
            let diffLocation = { x: maxLocation.x - minLocation.x, y: maxLocation.y - minLocation.y };
            // if diff x/y is too small
            if (diffLocation.x < 200) {
                minLocation.x -= (200 - diffLocation.x) / 2;
                diffLocation.x = 200;
            }
            if (diffLocation.y < 200) {
                minLocation.y -= (200 - diffLocation.y) / 2;
                diffLocation.y = 200;
            }

            let map = $("#mapImage");
            // scale to fit
            let rw = map.parent().width() / (diffLocation.x * 1.2 * this.mapRatio);
            let rh = map.parent().height() / (diffLocation.y * 1.2 * this.mapRatio);
            let ratio = rw < rh ? rw : rh;
            if (isFinite(ratio)) {
                this.mapRatio *= ratio * 0.9;
                map.width(map.width() * ratio * 0.9); // height is auto resized to keep the aspect ratio
            }

            // center
            map.css({
                position: "absolute",
                left: map.parent().width() / 2 - (minLocation.x + diffLocation.x / 2) * this.mapRatio,
                top: map.parent().height() / 2 - (minLocation.y + diffLocation.y / 2) * this.mapRatio
            });

            this.stickDevices(map);
        },
        moveMap: function (e) {
            if (e.buttons != 1) // left click
                return;

            let map = $("#mapImage");
            let pos = map.position();
            let left = Math.max(-map.width(), Math.min(pos.left + e.movementX, map.parent().width()));
            let top = Math.max(-map.height(), Math.min(pos.top + e.movementY, map.parent().height()));
            map.css({
                left: left,
                top: top,
            });

            this.stickDevices(map);
        },
        scaleMap: function (e) {
            if (this.edit || this.showDevice != null)
                return;

            let map = $("#mapImage");
            let pos = map.position();
            let centerX = pos.left + map.width() / 2;
            let centerY = pos.top + map.height() / 2;

            let ratio = 1 - (e.deltaY / 800);
            this.mapRatio *= ratio;
            map.width(map.width() * ratio); // height is auto resized to keep the aspect ratio

            let left = Math.max(-map.width(), Math.min(centerX - map.width() / 2, map.parent().width()));
            let top = Math.max(-map.height(), Math.min(centerY - map.height() / 2, map.parent().height()));
            map.css({
                left: left,
                top: top,
            });

            this.stickDevices(map);
        },
        stickDevices: function (map) {
            let mapPos = map.position();
            for (let room of this.rooms) {
                for (let device of room.Devices) {
                    let dev = $(`#${room.Name.replaceAll(' ', '_')}-${device.Name.replaceAll(' ', '_')}`);
                    dev.css({
                        position: "absolute",
                        left: mapPos.left + (device.Location.X * this.mapRatio) - dev.width() / 2,
                        top: mapPos.top + + (device.Location.Y * this.mapRatio) - dev.height() / 2
                    });
                }
            }
        },
        moveDevice: function (e) {
            if (e.buttons != 1) { // left click
                this.selected = null;
                return;
            }

            if (this.selected == null) {
                let idSplit = (e.target.id || e.target.parentElement.id || e.target.parentElement.parentElement.id).replaceAll("_", " ").split("-");
                let room = this.rooms.find(r => r.Name == idSplit[0]);
                if (room == null)
                    return;
                let device = room.Devices.find(d => d.Name == idSplit[1]);
                if (device == null)
                    return;
                // add debounced set function per device to set only after no changes in 1 sec
                this.selected = { room: room, device: device, setDevice: debounce(setDevice, 1000) };
            }

            let dev = $(`#${this.selected.room.Name.replaceAll(' ', '_')}-${this.selected.device.Name.replaceAll(' ', '_')}`);

            this.selected.device.Location.X = Math.round((e.pageX - $('#mapImage').offset().left) / this.mapRatio);
            this.selected.device.Location.Y = Math.round((e.pageY - $('#mapImage').offset().top) / this.mapRatio);
            this.selected.setDevice(this.selected.room.Name, this.selected.device.Name,
                { "Location": { "X": this.selected.device.Location.X, "Y": this.selected.device.Location.Y } });

            // move the UI element
            let mapPos = $("#mapImage").position();
            dev.css({
                position: "absolute",
                left: mapPos.left + (this.selected.device.Location.X * this.mapRatio) - dev.width() / 2,
                top: mapPos.top + + (this.selected.device.Location.Y * this.mapRatio) - dev.height() / 2
            });
        },

        click: function (room, device) {
            if (this.edit)
                return;

            if (device.ConfirmationRequired &&
                !confirm(`Are you sure you want to trigger the ${room.Name} ${device.Name}?`))
                return;

            if ("IsOn" in device) {
                device.IsOn = !device.IsOn;
                setDevice(room.Name, device.Name, { "IsOn": device.IsOn });
                return;
            }

            this.showDevice = { room: room, device: device };
        },

        save: function () {
            let obj = filterObjectBySettings(this.showDevice.device, false);
            let result = setDevice(this.showDevice.room.Name, this.showDevice.device.Name, obj);
            this.showDevice = null;
            return result;
        },

        stopPropagate: function (e) {
            if (this.edit)
                e.stopImmediatePropagation();
        },
    },
    mounted: function () {
        setTimeout(this.resetMap, 200); // wait to load devices
    },
    watch: {
        $route: function (to, from) {
            if (to.path != "/map")
                return;

            if (to.hash == "")
                this.resetMap();
            else
                this.zoomRoom(to.hash.slice(1));
        }
    }
});