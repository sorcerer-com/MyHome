setComponent("room-card", {
    props: ["room"],
    data: function () {
        return {
            selectedValueType: ""
        }
    },
    methods: {
        autoResizeFontSize: function () {
            // auto resize text font if it's larger
            $(".room-sensor-value").each((_, el) => {
                // if there is a unit text
                if (el.textContent.length > 3 && el.nextElementSibling.tagName == "SPAN")
                    $(el).removeClass("w3-xlarge");
                else if (el.textContent.length > 2 && el.nextElementSibling.tagName == "SPAN")
                    $(el).removeClass("w3-xlarge").addClass("w3-large");
                else
                    $(el).removeClass("w3-large").addClass("w3-xlarge");
            });
            $(".room-sensor-value-unit").each((_, el) => {
                if (el.textContent.length > 2)
                    $(el).addClass("w3-small");
                else
                    $(el).removeClass("w3-small");
            });
        },
        metadata: function (obj) {
            let result = "";
            for (let key in obj)
                result += `${key}: ${obj[key]}\n`;
            return result.trim();
        },
        isOffline: function (sensor) {
            return (new Date() - new Date(sensor.LastOnline)) > 60 * 60 * 1000; // if online before more than 1 hour in millis
        },
        getGroupedSensors: function () {
            return this.room.Devices.filter(d => d.$type.endsWith("Sensor") && d.Grouped);
        },
        getCameras: function () {
            return this.room.Devices.filter(d => d.$type.endsWith("Camera"));
        },
        getSensorsByValueType: function (valueType) {
            return this.room.Devices.filter(d => (d.$type.endsWith("Sensor") || d.$type.endsWith("Camera")) &&
                valueType in d.Values && !d.Grouped);
        },
        getSensorByName: function (name) {
            return this.room.Devices.find(d => d.$type.endsWith("Sensor") && d.Name == name);
        },
        getDrivers: function () {
            return this.room.Devices.filter(d => d.$type.endsWith("Driver"));
        },
        isGenericDriver: function (driver) {
            return !driver.$baseTypes.some(t => t.endsWith('IMediaDriver')) &&
                !driver.$baseTypes.some(t => t.endsWith('ISpeakerDriver'));
        },
        setRoomSecuritySystemEnabled: setRoomSecuritySystemEnabled
    },
    mounted: function () {
        this.autoResizeFontSize();

        if (this.$route.query.room == this.room.Name)
            this.selectedValueType = this.$route.query.selectedValueType;
    },
    watch: {
        room: function () {
            this.autoResizeFontSize();
        },
        selectedValueType: function () {
            if (this.selectedValueType == "") {
                // stop loading the motion image (cameras' images)
                $("img[id^='camera'][id$='Image']").attr("src", "");

                this.$router.push("/");
            }
            else
                this.$router.push({ query: { room: this.room.Name, selectedValueType: this.selectedValueType } });
        }
    }
});