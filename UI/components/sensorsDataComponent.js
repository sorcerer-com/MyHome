setComponent("sensors-data", {
    props: ["room", "sensors", "valueType"],
    data: function () {
        return {
            valueTypes: [],
            selection: "",
            sensorsData: {},
            stats: {},
            selectedMonth: "All",
            editorData: null,
            error: ""
        }
    },
    computed: {
        months: function () {
            const now = new Date();
            let result = ["All"];
            for (let i = 0; i < 12; i++)
                result.push(monthNames[(now.getMonth() - i + 12) % 12]);
            return result;
        },
        selectedPeriod: function () {
            const now = new Date();
            let periodEnd = now;
            periodEnd.setDate(periodEnd.getDate() - 1);
            periodEnd.setHours(0, 0, 0, 0);
            let periodStart = new Date(periodEnd);
            periodStart.setFullYear(periodStart.getFullYear() - 1);

            if (this.selectedMonth != "All") {
                let year = monthNames.indexOf(this.selectedMonth) <= now.getMonth() ?
                    now.getFullYear() : now.getFullYear() - 1;
                periodStart = new Date(year, monthNames.indexOf(this.selectedMonth));
                periodEnd = new Date(year, monthNames.indexOf(this.selectedMonth) + 1);
            }
            return [periodStart, periodEnd];
        }
    },
    methods: {
        refreshData: function (autorefresh = true) {
            this.charts = this.charts || {}; // not part of 'data' as it cause stack overflow
            this.charts["chartLastDay"] = this.charts["chartLastDay"] || createLineChart("chartLastDay", {}, false);
            this.charts["chartOlder"] = this.charts["chartOlder"] || createLineChart("chartOlder", {}, !window.vue.isMobile);

            const prevDay = new Date();
            prevDay.setDate(prevDay.getDate() - 1);
            prevDay.setMinutes(0, 0, 0);

            let allRequests = [];
            for (let sensor of this.sensors) {
                let name = this.valueType != null ? sensor.Name : this.selection;
                if (sensor.Units[this.selection])
                    name += ` (${sensor.Units[this.selection]})`;
                if (!this.stats[name]) {
                    // add empty values to preserve the order
                    this.stats[name] = { "LastDay": { Average: 0, Sum: 0 }, "Older": { Average: 0, Sum: 0 } };
                    updateChartData(this.charts["chartLastDay"], name, {});
                    updateChartData(this.charts["chartOlder"], name, {});
                }
                let request = getSensorData(this.room.Name, sensor.Name, this.selection).done(data => {
                    let lastDayData = Object.keys(data)
                        .map(k => { return { x: new Date(k), y: data[k] } })
                        .filter(e => e.x >= prevDay)
                        .sort((a, b) => (a.x > b.x) ? 1 : -1);
                    let olderData = Object.keys(data)
                        .map(k => { return { x: new Date(k), y: data[k] } })
                        .filter(e => e.x >= this.selectedPeriod[0] && e.x < this.selectedPeriod[1])
                        .sort((a, b) => (a.x > b.x) ? 1 : -1);
                    if (this.charts["chartLastDay"].data.datasets.some(ds => ds.label == name)) // ensure that this call isn't old
                        updateChartData(this.charts["chartLastDay"], name, lastDayData);
                    if (this.charts["chartOlder"].data.datasets.some(ds => ds.label == name)) // ensure that this call isn't old
                        updateChartData(this.charts["chartOlder"], name, olderData);

                    if (name in this.stats) {
                        this.stats[name] = {
                            "LastDay": {
                                Average: Math.round(lastDayData.reduce((sum, curr) => sum + curr.y, 0) / lastDayData.length * 100) / 100,
                                Sum: Math.round(lastDayData.reduce((sum, curr) => sum + curr.y, 0) * 100) / 100
                            }, "Older": {
                                Average: Math.round(olderData.reduce((sum, curr) => sum + curr.y, 0) / olderData.length * 100) / 100,
                                Sum: Math.round(olderData.reduce((sum, curr) => sum + curr.y, 0) * 100) / 100
                            }
                        };
                    }

                    this.sensorsData[name] = data;
                });
                allRequests.push(request);
            }

            if ((!window.ws || window.ws.readyState != WebSocket.OPEN) && autorefresh)
                $.when(...allRequests).done(() => setTimeout(this.refreshData, 3000)); // auto-refresh data every 3 seconds
        },
        showEditor: function () {
            let ordered = {};
            for (let name of Object.keys(this.sensorsData)) {
                ordered[name] = Object.keys(this.sensorsData[name]).sort().reduce(
                    (obj, key) => {
                        obj[key] = this.sensorsData[name][key]; return obj;
                    }, {});
            }
            this.editorData = this.editorData ? null : JSON.stringify(ordered, null, 2);
            this.error = "";
        },
        save: function () {
            let data = JSON.parse(this.editorData);
            let allRequests = [];
            for (let sensor of this.sensors) {
                let name = this.valueType != null ? sensor.Name : this.selection;
                if (sensor.Units[this.selection])
                    name += ` (${sensor.Units[this.selection]})`;
                allRequests.push(setSensorData(this.room.Name, sensor.Name, this.selection, data[name]));
            }
            $.when(...allRequests).done(() => this.editorData = null)
                .fail(response => this.error = "Error: " + response.responseText);

        }
    },
    mounted: function () {
        this.valueTypes = this.valueType != null ? [this.valueType] : Object.keys(this.sensors[0].Values);
        this.selection = this.valueTypes[0];

        window.ws?.addRefreshHandlers(this.refreshData);
    },
    unmounted: function () {
        window.ws?.removeRefreshHandlers(this.refreshData);
    },
    watch: {
        selection: function () {
            if (this.charts) {
                this.charts["chartLastDay"]?.destroy();
                this.charts["chartOlder"]?.destroy();
            }

            this.sensorsData = {};
            this.charts = {};
            this.stats = {};
            this.selectedMonth = "All";
            this.editorData = null;

            this.refreshData(false);
        },
        selectedMonth: function () {
            this.refreshData(false);
        }
    }
});