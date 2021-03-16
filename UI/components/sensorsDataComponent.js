var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("sensors-data", {
        template: template,
        props: ["sensors", "valueType"],
        methods: {
            refreshData: function () {
                let allRequests = [];
                for (let sensor of this.sensors) {
                    let request = getSensorData(sensor.Name, this.valueType).done(data => {
                        let lastDayData = Object.keys(data.lastDay).map(k => { return { t: new Date(k), y: data.lastDay[k] } });
                        let lastYearData = Object.keys(data.lastYear).map(k => { return { t: new Date(k), y: data.lastYear[k] } });
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
                $.when(allRequests).done(() => setTimeout(this.refreshData, 3000)); // auto-refresh data every 3 seconds
            }
        },
        data: function () {
            return {
                sensorsData: {},
                charts: {}
            }
        },
        created: function () {
            this.refreshData();
        },
        destroyed: function () {
            this.sensors = [];
        }
    });
});