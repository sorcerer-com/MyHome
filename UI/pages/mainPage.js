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
                modalSelection: "",
                charts: {},
                stats: {}
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

            showChartsModal: function () {
                this.modal = "Charts";
                this.modalSelection = this.rooms[0].Name;
            },

            showSecurityModal: function () {
                this.modal = "Security";
                // wait canvas to show
                setTimeout(() => {
                    if (!this.charts["chartSecurity"])
                        this.charts["chartSecurity"] = createLineChart("chartSecurity");

                    for (let room of this.rooms) {
                        let chartData = Object.keys(room.SecurityHistory)
                            .map(k => {
                                return {
                                    t: new Date(k),
                                    y: room.SecurityHistory[k] == "Enabled" ? 1 : (room.SecurityHistory[k] == "Activated" ? 2 : 0)
                                }
                            })
                            .sort((a, b) => (a.t > b.t) ? 1 : -1);

                        updateChartData(this.charts["chartSecurity"], room.Name, chartData);

                        Vue.set(this.stats, room.Name, {
                            Average: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) / chartData.length * 100) / 100,
                            Sum: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) * 100) / 100
                        });
                    }
                }, 10);
            }
        },
        mounted: function () {
            this.refreshData();
            window.ws?.addRefreshHandlers(this.refreshData);
        },
        watch: {
            modal: function (value) {
                if (value == "") {
                    this.charts = {};
                    this.modalSelection = "";
                    this.stats = {};
                }
            },
            modalSelection: function (value) {
                if (this.modal == "Charts") {
                    // wait canvas to show
                    setTimeout(() => {
                        this.charts["charts"]?.destroy();
                        this.charts["charts"] = createLineChart("charts");
                        this.stats = {};

                        const prevDay = new Date();
                        prevDay.setDate(prevDay.getDate() - 1);
                        prevDay.setUTCMinutes(0, 0, 0);

                        let room = this.rooms.find(r => r.Name == value);
                        for (let sensor of room.Devices.filter(d => d.$type.endsWith("Sensor") || d.$type.endsWith("Camera"))) {
                            for (let valueType of Object.keys(sensor.Values)) {
                                getSensorData(room.Name, sensor.Name, valueType).done(data => {
                                    let chartData = Object.keys(data)
                                        .map(k => { return { t: new Date(k), y: data[k] } })
                                        .filter(e => e.t < prevDay)
                                        .sort((a, b) => (a.t > b.t) ? 1 : -1);
                                    updateChartData(this.charts["charts"], `${sensor.Name}.${valueType}`, chartData);

                                    Vue.set(this.stats, `${sensor.Name}.${valueType}`, {
                                        Average: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) / chartData.length * 100) / 100,
                                        Sum: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) * 100) / 100
                                    });
                                });
                            }
                        }
                    }, 10);
                }
            }
        }
    });
});