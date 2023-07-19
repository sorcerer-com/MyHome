setComponent("security-list", {
    props: ["rooms"],
    data: function () {
        return {
        }
    },
    methods: {
        setRoomSecuritySystemEnabled: setRoomSecuritySystemEnabled,
        setAllRoomsSecuritySystemEnabled: function (isEnabled) {
            for (let room of this.rooms) {
                if (room.IsSecuritySystemEnabled != isEnabled)
                    setRoomSecuritySystemEnabled(room.Name, isEnabled);
            }
        },

        showModal: function () {
            this.$emit("show-modal");
        }
    }
});