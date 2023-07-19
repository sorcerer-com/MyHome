setComponent("main-page", {
    data: function () {
        return {
            rooms: [],
            security: {},

            modal: "",
            modalSelection: "",
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
                this.rooms = rooms;
                // update security modal if opened
                if (this.modal == "Security")
                    this.showSecurityModal();
            });

            getSystem("Security").done(security => this.security = security);

            if (!window.ws || window.ws.readyState != WebSocket.OPEN)
                setTimeout(this.refreshData, 3000);
        },

        showChartsModal: function () {
            this.modal = "Charts";
            this.modalSelection = this.rooms[0].Name;
        },

        showSecurityModal: function () {
            this.modal = "Security";
            // wait canvas to show
            setTimeout(() => {
                this.charts = this.charts || {};
                this.charts["chartSecurity"] = this.charts["chartSecurity"] || createLineChart("chartSecurity");

                for (let room of this.rooms) {
                    let chartData = Object.keys(room.SecurityHistory)
                        .map(k => {
                            return {
                                x: new Date(k),
                                y: room.SecurityHistory[k] == "Enabled" ? 1 : (room.SecurityHistory[k] == "Activated" ? 2 : 0)
                            }
                        })
                        .sort((a, b) => (a.x > b.x) ? 1 : -1);

                    updateChartData(this.charts["chartSecurity"], room.Name, chartData);

                    this.stats[room.Name] = {
                        Average: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) / chartData.length * 100) / 100,
                        Sum: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) * 100) / 100
                    };
                }

                for (let name of Object.keys(this.security.PresenceDeviceIPs)) {
                    if (!(name in this.security.History))
                        continue;
                    let chartData = Object.keys(this.security.History[name])
                        .map(k => {
                            return {
                                x: new Date(k),
                                y: this.security.History[name][k] == "Present" ? 1 : 0
                            }
                        })
                        .sort((a, b) => (a.x > b.x) ? 1 : -1);

                    updateChartData(this.charts["chartSecurity"], name, chartData);

                    this.stats[name] = {
                        Average: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) / chartData.length * 100) / 100,
                        Sum: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) * 100) / 100
                    };
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
                    this.charts = this.charts || {};
                    this.charts["charts"]?.destroy();
                    this.charts["charts"] = createLineChart("charts");
                    this.stats = {};

                    const prevDay = new Date();
                    prevDay.setDate(prevDay.getDate() - 1);
                    prevDay.setUTCMinutes(0, 0, 0);

                    let room = this.rooms.find(r => r.Name == value);
                    for (let sensor of room.Devices.filter(d => d.$type.endsWith("Sensor") || d.$type.endsWith("Camera"))) {
                        for (let valueType of Object.keys(sensor.Values)) {
                            if (!this.stats[`${sensor.Name}.${valueType}`]) {
                                // add empty values to preserve the order
                                this.stats[`${sensor.Name}.${valueType}`] = { Average: 0, Sum: 0 };
                                updateChartData(this.charts["charts"], `${sensor.Name}.${valueType}`, {});
                            }
                            getSensorData(room.Name, sensor.Name, valueType).done(data => {
                                let chartData = Object.keys(data)
                                    .map(k => { return { x: new Date(k), y: data[k] } })
                                    .filter(e => e.x < prevDay)
                                    .sort((a, b) => (a.x > b.x) ? 1 : -1);
                                updateChartData(this.charts["charts"], `${sensor.Name}.${valueType}`, chartData);

                                this.stats[`${sensor.Name}.${valueType}`] = {
                                    Average: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) / chartData.length * 100) / 100,
                                    Sum: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) * 100) / 100
                                };
                            });
                        }
                    }
                    this.charts['charts'].options.maintainAspectRatio = false;
                    this.charts['charts'].options.plugins.legend.position = "bottom";
                    this.charts['charts'].canvas.style["max-height"] = this.charts["charts"].height + this.charts["charts"].legend.height + "px";
                    this.charts['charts'].resize();
                }, 10);
            }
        }
    }
});