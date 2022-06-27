var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    const monthNames = ["January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"];

    Vue.component("sensors-data", {
        template: template,
        props: ["room", "sensors", "valueType"],
        data: function () {
            return {
                sensorsData: {},
                charts: {},
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
                periodStart.setFullYear(periodStart.getFullYear() - 2);

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
                if (this._isDestroyed) {
                    window.ws?.removeRefreshHandlers(this.refreshData);
                    return;
                }

                if (!this.charts["chartLastDay"])
                    this.charts["chartLastDay"] = createLineChart("chartLastDay", {}, false);
                if (!this.charts["chartOlder"])
                    this.charts["chartOlder"] = createLineChart("chartOlder");

                const prevDay = new Date();
                prevDay.setDate(prevDay.getDate() - 1);
                prevDay.setMinutes(0, 0, 0);

                let allRequests = [];
                for (let sensor of this.sensors) {
                    let request = getSensorData(this.room.Name, sensor.Name, this.valueType).done(data => {
                        let lastDayData = Object.keys(data)
                            .map(k => { return { t: new Date(k), y: data[k] } })
                            .filter(e => e.t >= prevDay)
                            .sort((a, b) => (a.t > b.t) ? 1 : -1);
                        let olderData = Object.keys(data)
                            .map(k => { return { t: new Date(k), y: data[k] } })
                            .filter(e => e.t >= this.selectedPeriod[0] && e.t < this.selectedPeriod[1])
                            .sort((a, b) => (a.t > b.t) ? 1 : -1);
                        updateChartData(this.charts["chartLastDay"], sensor.Name, lastDayData);
                        updateChartData(this.charts["chartOlder"], sensor.Name, olderData);

                        Vue.set(this.stats, sensor.Name, {
                            "LastDay": {
                                Average: Math.round(lastDayData.reduce((sum, curr) => sum + curr.y, 0) / lastDayData.length * 100) / 100,
                                Sum: Math.round(lastDayData.reduce((sum, curr) => sum + curr.y, 0) * 100) / 100
                            }, "Older": {
                                Average: Math.round(olderData.reduce((sum, curr) => sum + curr.y, 0) / olderData.length * 100) / 100,
                                Sum: Math.round(olderData.reduce((sum, curr) => sum + curr.y, 0) * 100) / 100
                            }
                        });

                        this.sensorsData[sensor.Name] = data;
                    });
                    allRequests.push(request);
                }

                if ((!window.ws || window.ws.readyState != WebSocket.OPEN) && autorefresh)
                    $.when(...allRequests).done(() => setTimeout(this.refreshData, 3000)); // auto-refresh data every 3 seconds
            },
            showEditor: function () {
                this.editorData = this.editorData ? null : JSON.stringify(this.sensorsData, null, 2);
                this.error = "";
            },
            save: function () {
                let data = JSON.parse(this.editorData);
                let allRequests = [];
                for (let sensor of this.sensors) {
                    allRequests.push(setSensorData(this.room.Name, sensor.Name, this.valueType, data[sensor.Name]));
                }
                $.when(...allRequests).done(() => this.editorData = null)
                    .fail(response => this.error = "Error: " + response.responseText);

            }
        },
        mounted: function () {
            this.refreshData();
            window.ws?.addRefreshHandlers(this.refreshData);
        },
        watch: {
            selectedMonth: function (value) {
                this.refreshData(false);
            }
        }
    });
});