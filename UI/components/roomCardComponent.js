var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("room-card", {
        template: template,
        props: ["room"],
        methods: {
            metadata: function (obj) {
                let result = "";
                for (let key in obj)
                    result += `${key}: ${obj[key]}\n`;
                return result.trim();
            },
            getCameras: function () {
                return this.room.Devices.filter(d => d.$type.endsWith("Camera"));
            },
            getSensorsBySelectedValueType: function () {
                return this.room.Devices.filter(d => this.selectedValueType in d.LastValues);
            },

            setRoomSecuritySystemEnabled: setRoomSecuritySystemEnabled
        },
        data: function () {
            return {
                selectedValueType: ""
            }
        }
    });
});