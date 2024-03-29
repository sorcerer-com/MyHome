﻿setComponent("config-page", {
    data: function () {
        return {
            rooms: [],
            system: null,

            edit: {
                name: null,
                types: null,
                object: null,
                roomName: null,
                discovered: null,
                onSave: null,
                onDelete: null
            }
        }
    },
    methods: {
        refreshData: function () {
            getRooms().done(rooms => this.rooms = rooms);
            getSystem("Devices").done(system => this.system = system);

            if (!window.ws || window.ws.readyState != WebSocket.OPEN)
                setTimeout(this.refreshData, 3000);
        },

        showEdit: function (roomName, object, discovered, onSave, onDelete) {
            this.edit.name = object.Name;
            this.edit.types = null;
            this.edit.object = object;
            this.edit.roomName = roomName;
            this.edit.discovered = discovered;
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
            getSubTypes("Device").done(types => {
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
                return $.Deferred().reject({ responseText: "Cannot set room without name" });
            if (this.edit.name == "Add Room" && this.rooms.some(r => r != room && r.Name == room.Name))
                return $.Deferred().reject({ responseText: "A room with the same name already exists" });

            let settings = filterObjectBySettings(room, true);
            return setRoom(this.edit.roomName, settings).done(() => this.edit.name = null);
        },
        saveDevice: function (device) {
            if (device.Name == "")
                return $.Deferred().reject({ responseText: "Cannot set device without name" });
            if (this.edit.roomName == null)
                return $.Deferred().reject({ responseText: "Cannot set device without selected room" });
            let room = this.rooms.find(r => r.Name == this.edit.roomName);
            if (this.edit.name == "Add Device" && room.Devices.some(d => d != device && d.Name == device.Name))
                return $.Deferred().reject({ responseText: "A device with the same name already exists" });

            if (this.edit.discovered)
                callSystem("Devices", "RemoveDiscoveredDevice", this.edit.discovered);

            let settings = filterObjectBySettings(device, true);
            return setDevice(this.edit.roomName, this.edit.name, settings).done(() => this.edit.name = null);
        },

        cloneDevice: function () {
            this.edit.name = this.edit.name + " - Clone";
            this.edit.object = { ...this.edit.object }; // clone the object
            this.edit.object.Name = this.edit.name;
        },

        deleteRoom: function () {
            if (!confirm(`Are you sure you want to delete the ${this.edit.roomName} and all its devices ?`))
                return;

            deleteRoom(this.edit.roomName).done(() => this.edit.name = null);
        },
        deleteDevice: function () {
            if (!confirm(`Are you sure you want to delete the ${this.edit.roomName} ${this.edit.name}?`))
                return;

            deleteDevice(this.edit.roomName, this.edit.name).done(() => this.edit.name = null);
        }
    },
    mounted: function () {
        this.refreshData();
        window.ws?.addRefreshHandlers(this.refreshData);
    },
    unmounted: function () {
        window.ws?.removeRefreshHandlers(this.refreshData);
    }
});