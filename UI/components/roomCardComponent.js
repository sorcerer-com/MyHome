var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("room-card", {
        template: template,
        props: ["room"],
        data: function () {
            return {
                selectedValueType: "",
                securityChart: null
            }
        },
        methods: {
            autoResizeFontSize: function () {
                // auto resize text font if it's larger
                $(".room-sensor-value").each((_, el) => {
                    if (el.textContent.length > 2 && el.nextElementSibling.tagName == "SPAN") // if there is a unit text
                        $(el).removeClass("w3-xlarge").addClass("w3-large");
                    else
                        $(el).removeClass("w3-large").addClass("w3-xlarge");
                });
                $(".room-sensor-value-unit").each((_, el) => {
                    if (el.textContent.length > 2)
                        $(el).addClass("w3-small");
                    else
                        $(el).removeClass("w3-small");
                });
            },
            refreshSecurityHistory: function () {
                if (this.selectedValueType != 'Security') {
                    this.securityChart = null;
                    return;
                }

                let data = Object.keys(this.room.SecurityHistory)
                    .map(k => {
                        return {
                            t: new Date(k),
                            y:this.room.SecurityHistory[k] == "Enabled" ? 1 : (this.room.SecurityHistory[k] == "Activated" ? 2 : 0)
                        }
                    })
                    .sort((a, b) => (a.t > b.t) ? 1 : -1);
                if (!this.securityChart)
                    this.securityChart = showLineChart("securityChart", data, "SecurityHistory");
                else
                    updateChartData(this.securityChart, data);
            },
            metadata: function (obj) {
                let result = "";
                for (let key in obj)
                    result += `${key}: ${obj[key]}\n`;
                return result.trim();
            },
            getCameras: function () {
                return this.room.Devices.filter(d => d.$type.endsWith("Camera"));
            },
            getSensorsBySelectedValueType: function () {
                return this.room.Devices.filter(d => (d.$type.endsWith("Sensor") || d.$type.endsWith("Camera")) &&
                    this.selectedValueType in d.LastValues);
            },
            getDrivers: function () {
                return this.room.Devices.filter(d => d.$type.endsWith("Driver"));
            },

            setRoomSecuritySystemEnabled: setRoomSecuritySystemEnabled
        },
        mounted: function () {
            this.autoResizeFontSize();
        },
        watch: {
            room: function () {
                this.autoResizeFontSize();
                this.refreshSecurityHistory();
            },
            selectedValueType: function () {
                setTimeout(() => this.refreshSecurityHistory(), 10); // wait canvas to show
            }
        }
    });
});