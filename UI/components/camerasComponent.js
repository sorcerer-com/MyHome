setComponent("cameras", {
    props: ["room", "cameras"],
    data: function () {
        return {
        }
    },
    methods: {
        moveCamera: moveCamera,
        restartCamera: function (roomName, cameraName) {
            if (!confirm(`Are you sure you want to restart the ${roomName} ${cameraName}?`))
                return;

            restartCamera(roomName, cameraName);
        }
    }
});