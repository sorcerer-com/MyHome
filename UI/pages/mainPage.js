var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("main-page", {
        template: template,
        data: function () {
            return {
                rooms: [],
                mediaPlayer: {},

                mediaPlayerHover: false,

                modal: "",
                charts: {}
            }
        },
        computed: {
            allRoomsSecuritySystemEnabled: function () {
                return this.rooms.length > 0 && this.rooms.every(r => r.IsSecuritySystemEnabled)
            },
            someRoomSecuritySystemEnabled: function () {
                return this.rooms.some(r => r.IsSecuritySystemEnabled) && !this.rooms.every(r => r.IsSecuritySystemEnabled)
            },
        },
        methods: {
            refreshData: function () {
                if (this._isDestroyed) {
                    window.ws?.removeRefreshHandlers(this.refreshData);
                    return;
                }

                getRooms().done(rooms => {
                    Vue.set(this, "rooms", rooms);
                    // update security modal if opened
                    if (this.modal == "Security")
                        this.showSecurityModal();
                });

                getSystem("MediaPlayer").done(mediaPlayer => Vue.set(this, "mediaPlayer", mediaPlayer));

                if (!window.ws || window.ws.readyState != WebSocket.OPEN || this.mediaPlayerHover)
                    setTimeout(this.refreshData, 3000);
            },

            hoverMediaPlayer: function () {
                if (!this.mediaPlayerHover) {
                    this.mediaPlayerHover = true;
                    this.refreshData();
                }
            },

            showSecurityModal: function () {
                this.modal = "Security";
                // TODO: merge all chart in one?
                // TODO: add modal with all sensors data

                for (let room of this.rooms) {
                    let data = Object.keys(room.SecurityHistory)
                        .map(k => {
                            return {
                                t: new Date(k),
                                y: room.SecurityHistory[k] == "Enabled" ? 1 : (room.SecurityHistory[k] == "Activated" ? 2 : 0)
                            }
                        })
                        .sort((a, b) => (a.t > b.t) ? 1 : -1);

                    let chartName = "chart" + room.Name.replace(" ", "") + "Security";
                    if (!this.charts[room.chartName])
                        // wait canvas to show
                        setTimeout(() => this.charts[room.chartName] = createLineChart(chartName, { "SecurityHistory": data }), 10);
                    else
                        updateChartData(this.charts[room.chartName], room.chartName, data);
                }
            }
        },
        mounted: function () {
            this.refreshData();
            window.ws?.addRefreshHandlers(this.refreshData);
        },
        watch: {
            modal: function (value) {
                if (value == "")
                    this.charts = {};
            }
        }
    });
});