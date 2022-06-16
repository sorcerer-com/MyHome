var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("main-page", {
        template: template,
        data: function () {
            return {
                rooms: [],
                mediaPlayer: {},

                mediaPlayerHover: false
            }
        },
        methods: {
            refreshData: function () {
                if (this._isDestroyed) {
                    window.ws?.removeRefreshHandlers(this.refreshData);
                    return;
                }

                getRooms().done(rooms => Vue.set(this, "rooms", rooms));

                // update media player if it is opened
                if (this.mediaPlayerHover) {
                    getSystem("MediaPlayer").done(mediaPlayer => Vue.set(this, "mediaPlayer", mediaPlayer));
                }

                if (!window.ws || window.ws.readyState != WebSocket.OPEN || this.mediaPlayerHover)
                    setTimeout(this.refreshData, 3000);
            },

            allRoomsSecuritySystemEnabled: function () {
                return this.rooms.length > 0 && this.rooms.every(r => r.IsSecuritySystemEnabled)
            },
            someRoomSecuritySystemEnabled: function () {
                return this.rooms.some(r => r.IsSecuritySystemEnabled) && !this.rooms.every(r => r.IsSecuritySystemEnabled)
            },

            hoverMediaPlayer: function () {
                if (!this.mediaPlayerHover) {
                    this.mediaPlayerHover = true;
                    this.refreshData();
                }
            }
        },
        mounted: function () {
            this.refreshData();
            getSystem("MediaPlayer").done(mediaPlayer => Vue.set(this, "mediaPlayer", mediaPlayer));
            window.ws?.addRefreshHandlers(this.refreshData);
        }
    });
});