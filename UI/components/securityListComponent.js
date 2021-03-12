var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component('security-list', {
        template: template,
        props: ['rooms'],
        data: function () {
            return {
            }
        },
        methods: {
            setRoomSecuritySystemEnabled: function (roomName, isEnabled) {
                setRoom(roomName, { IsSecuritySystemEnabled: isEnabled })
                    .done(() => getRooms().done(rooms => updateVue({ rooms: rooms })));
            },
            setAllRoomsSecuritySystemEnabled: function (isEnabled) {
                for (let room of this.rooms)
                    this.setRoomSecuritySystemEnabled(room.Name, isEnabled);
            },
        }
    });
});