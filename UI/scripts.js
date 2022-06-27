function setRoomSecuritySystemEnabled(roomName, isEnabled) {
    setRoom(roomName, { IsSecuritySystemEnabled: isEnabled })
        .done(() => getRooms().done(rooms => Vue.set(this.$parent, "rooms", rooms)));
}


function createLineChart(canvasId, datasets = {}, allowZoom = true) { // datasets: {label: data}
    let colors = generateChartColors(Object.keys(datasets).length);
    let chartDataSets = Object.keys(datasets).map((k, idx) => {
        return {
            label: k,
            data: datasets[k],
            backgroundColor: colors[idx].alpha(0.1).rgbString(),
            borderColor: colors[idx].alpha(0.5).rgbString(),
            type: "line",
            lineTension: 0,
            pointRadius: 0,
            borderWidth: 2
        }
    });
    let cfg = {
        data: {
            datasets: chartDataSets
        },
        options: {
            scales: {
                xAxes: [{
                    type: "time",
                    ticks: {
                        fontColor: "white"
                    },
                    time: {
                        displayFormats: {
                            hour: "HH"
                        },
                        tooltipFormat: "DD MMM YYYY, HH:mm:ss"
                    }
                }],
                yAxes: [{
                    ticks: {
                        fontColor: "white"
                    }
                }]
            },
            tooltips: {
                intersect: false,
                mode: "index"
            },
            legend: {
                labels: {
                    fontColor: "white"
                }
            },
            /* Zoom plugin */
            plugins: {
                zoom: {
                    pan: {
                        enabled: allowZoom,
                        mode: "x"
                    },
                    zoom: {
                        enabled: allowZoom,
                        mode: "x"
                    }
                }
            }
        }
    };

    let chart = new Chart(canvasId, cfg);
    document.getElementById(canvasId).ondblclick = () => chart.resetZoom();
    return chart;
}

function updateChartData(chart, label, data) {
    chart.resetZoom();
    let dataset = chart.data.datasets.find(ds => ds.label == label);
    if (dataset) {
        dataset.data = data;
    } else {
        chart.data.datasets.push({
            label: label,
            data: data,
            type: "line",
            lineTension: 0,
            pointRadius: 0,
            borderWidth: 2
        });
        // update colors
        let colors = generateChartColors(chart.data.datasets.length);
        for (let i = 0; i < colors.length; i++) {
            chart.data.datasets[i].backgroundColor = colors[i].alpha(0.1).rgbString();
            chart.data.datasets[i].borderColor = colors[i].alpha(0.5).rgbString();
        }
    }
    chart.update();
}

function generateChartColors(count) {
    let dHue = 360 / Math.min(count, 10);
    let dValue = 100 / Math.ceil(count / 10);
    let result = [];
    for (let v = 0; v < 100; v += dValue) {
        for (let h = 0; h < 360; h += dHue) {
            if (result.length >= count)
                return result;
            result.push(Color().hsv((240 + h) % 360, 100, 100 - v));
        }
    }
    return result;
}


function splitTypes(list) {
    let split = list.split(", ");
    for (let i = 0; i < split.length; i++) {
        if (split[i].includes("<") && !split[i].includes(">")) {
            split[i] += ", " + split[i + 1];
            split.splice(i + 1);
            i--;
        }
    }
    return split;
}


// execute func once after no call for delay period of time
const debounce = function (func, delay) {
    let timer;
    return function () {     //anonymous function
        const context = this;
        const args = arguments;
        clearTimeout(timer);
        timer = setTimeout(() => {
            func.apply(context, args)
        }, delay);
    }
}