function setRoomSecuritySystemEnabled(roomName, isEnabled) {
    setRoom(roomName, { IsSecuritySystemEnabled: isEnabled })
        .done(() => getRooms().done(rooms => Vue.set(this.$parent, "rooms", rooms)));
}

function filterObjectBySettings(object, settings) {
    return Object.keys(object)
        .filter(key => key[0] == "$" || settings == null || object['$subtypes'][key].setting == settings)
        .reduce((cur, key) => {
            let value = object[key] && object[key]['$subtypes'] ? filterObjectBySettings(object[key]) : object[key];
            return Object.assign(cur, {[key]: value})
        }, {});
}


function createLineChart(canvasId, datasets = {}, allowZoom = true) { // datasets: {label: data}
    let colors = generateChartColors(Object.keys(datasets).length);
    let chartDataSets = Object.keys(datasets).map((k, idx) => {
        return {
            label: k,
            data: datasets[k],
            backgroundColor: `hsla(${colors[idx]}, 0.1)`,
            borderColor: `hsla(${colors[idx]}, 0.5)`,
            tension: 0,
            pointRadius: 0,
            borderWidth: 2,
            fill: true
        }
    });
    let cfg = {
        type: 'line',
        data: {
            datasets: chartDataSets
        },
        options: {
            scales: {
                x: {
                    type: "time",
                    ticks: {
                        color: "white"
                    },
                    time: {
                        displayFormats: {
                            hour: "HH"
                        },
                        tooltipFormat: "DD MMM YYYY, HH:mm:ss"
                    }
                },
                y: {
                    ticks: {
                        color: "white"
                    }
                }
            },
            plugins: {
                colors: {
                    forceOverride: true,
                    enabled: false
                },
                legend: {
                    labels: {
                        color: "white"
                    }
                },
                tooltip: {
                    intersect: false,
                    mode: "index"
                },
                zoom: {
                    pan: {
                        enabled: allowZoom,
                        mode: "x"
                    },
                    zoom: {
                        wheel: {
                            enabled: allowZoom
                        },
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

function updateChartData(chart, label, data) { // data: {x: date, y: value}
    let dataset = chart.data.datasets.find(ds => ds.label == label);
    if (dataset) {
        if (dataset.data.length != data.length) // if data get changed
            chart.resetZoom();
        dataset.data = data;
    } else {
        chart.resetZoom();
        chart.data.datasets.push({
            label: label,
            data: data,
            tension: 0,
            pointRadius: 0,
            borderWidth: 2,
            fill: true
        });
        // update colors
        let colors = generateChartColors(chart.data.datasets.length);
        for (let i = 0; i < colors.length; i++) {
            chart.data.datasets[i].backgroundColor = `hsla(${colors[i]}, 0.1)`;
            chart.data.datasets[i].borderColor = `hsla(${colors[i]}, 0.5)`;
        }
    }
    chart.update();
}

function generateChartColors(count) {
    let dHue = 360 / Math.min(count, 10);
    let dLight = 50 / Math.ceil(count / 10);
    let result = [];
    for (let l = 0; l < 50; l += dLight) {
        for (let h = 0; h < 360; h += dHue) {
            if (result.length >= count)
                return result;
            result.push(`${(240 + h) % 360}, 100%, ${50 - l}%`); // HSL
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

function dateToString(date) {
    return `${date.getDate()}/`.padStart(3, '0') + `${date.getMonth() + 1}/`.padStart(3, '0') + `${date.getFullYear()} ` +
        `${date.getHours()}:`.padStart(3, '0') + `${date.getMinutes()}:`.padStart(3, '0') + `${date.getSeconds()}`.padStart(2, '0');
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