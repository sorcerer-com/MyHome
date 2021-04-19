var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("config-page", {
        template: template,
        data: function () {
            return {
                rooms: [],
                devices: [],

                edit: {
                    name: null,
                    types: null,
                    object: null,
                    roomName: null,
                    onSave: null,
                    onDelete: null
                }
            }
        },
        methods: {
            refreshData: function () {
                if (this._isDestroyed)
                    return;

                getRooms(true).done(rooms => {
                    Vue.set(this, "rooms", rooms);
                    setTimeout(this.refreshData, 3000);
                }).fail(() => {
                    setTimeout(this.refreshData, 1000);
                });
            },

            showEdit: function (roomName, object, onSave, onDelete) {
                this.edit.name = object.Name;
                this.edit.types = null;
                this.edit.object = object;
                this.edit.roomName = roomName;
                this.edit.onSave = onSave;
                this.edit.onDelete = onDelete;
            },
            showAddRoom: function () {
                createRoom().done(room => {
                    this.edit.name = "Add Room";
                    this.edit.types = null;
                    this.edit.object = room;
                    this.edit.roomName = "new_room";
                    this.edit.onSave = this.saveRoom;
                    this.edit.onDelete = null;
                });
            },
            showAddDevice: function (room) {
                getDeviceTypes().done(types => {
                    this.edit.name = "Add Device";
                    this.edit.types = types;
                    this.edit.object = null;
                    this.edit.roomName = room.Name;
                    this.edit.onSave = null;
                    this.edit.onDelete = null;
                });
            },
            onTypeChange: function (event) {
                createDevice(this.edit.roomName, event.target.value).done(device => {
                    this.edit.object = device;
                    this.edit.onSave = this.saveDevice;
                });
            },

            saveRoom: function (room) {
                if (room.Name == "")
                    return $.Deferred().reject({ responseText: "Cannot add room without name" });
                if (this.rooms.some(r => r.Name = room.Name))
                    return $.Deferred().reject({ responseText: "A room with the same name already exists" });

                return setRoom(this.edit.roomName, room).done(() => this.edit.name = null);
            },
            saveDevice: function (device) {
                if (device.Name == "")
                    return $.Deferred().reject({ responseText: "Cannot add device without name" });
                let room = this.rooms.find(r => r.Name == this.edit.roomName);
                if (room.Devices.some(d => d.Name = device.Name))
                    return $.Deferred().reject({ responseText: "A device with the same name already exists" });

                return setDevice(this.edit.roomName, this.edit.name, device).done(() => this.edit.name = null);
            },

            deleteRoom: function () {
                if (!confirm("Are you sure you want to delete the room and all its devices?"))
                    return;

                deleteRoom(this.edit.roomName).done(() => this.edit.name = null);
            },
            deleteDevice: function () {
                if (!confirm("Are you sure you want to delete the device?"))
                    return;

                deleteDevice(this.edit.roomName, this.edit.name).done(() => this.edit.name = null);
            }
        },
        mounted: function () {
            this.refreshData();
        }
    });
});