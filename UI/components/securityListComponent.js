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
});