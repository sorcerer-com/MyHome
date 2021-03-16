var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("security-list", {
        template: template,
        props: ["rooms"],
        data: function () {
            return {
            }
        },
        methods: {
            setRoomSecuritySystemEnabled: setRoomSecuritySystemEnabled,
            setAllRoomsSecuritySystemEnabled: function (isEnabled) {
                for (let room of this.rooms)
                    this.setRoomSecuritySystemEnabled(room.Name, isEnabled);
            },
        }
    });
});