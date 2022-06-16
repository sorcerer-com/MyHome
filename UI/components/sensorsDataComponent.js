var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("sensors-data", {
        template: template,
        props: ["room", "sensors", "valueType"],
        data: function () {
            return {
                sensorsData: {},
                charts: {},
                editorData: null,
                error: ""
            }
        },
        methods: {
            refreshData: function () {
                if (this._isDestroyed) {
                    window.ws?.removeRefreshHandlers(this.refreshData);
                    return;
                }

                let allRequests = [];
                for (let sensor of this.sensors) {
                    let request = getSensorData(this.room.Name, sensor.Name, this.valueType).done(data => {
                        let lastDayData = Object.keys(data.lastDay).map(k => { return { t: new Date(k), y: data.lastDay[k] } }).sort((a, b) => (a.t > b.t) ? 1 : -1);
                        let lastYearData = Object.keys(data.lastYear).map(k => { return { t: new Date(k), y: data.lastYear[k] } }).sort((a, b) => (a.t > b.t) ? 1 : -1);
                        if (!(sensor.Name in this.sensorsData)) { // init
                            this.charts[`chart${sensor.Name}LastDay`] = showLineChart(`chart${sensor.Name}LastDay`, lastDayData, "Last Day");
                            this.charts[`chart${sensor.Name}LastYear`] = showLineChart(`chart${sensor.Name}LastYear`, lastYearData, "Last Year");
                        } else { // update
                            updateChartData(this.charts[`chart${sensor.Name}LastDay`], lastDayData);
                            updateChartData(this.charts[`chart${sensor.Name}LastYear`], lastYearData);
                        }

                        this.sensorsData[sensor.Name] = data;
                    });
                    allRequests.push(request);
                }

                if (!window.ws || window.ws.readyState != WebSocket.OPEN)
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
        }
    });
});