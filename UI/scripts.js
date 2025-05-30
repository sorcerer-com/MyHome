﻿const monthNames = ["January", "February", "March", "April", "May", "June",
    "July", "August", "September", "October", "November", "December"];

function setComponent(name, component) {
    let scriptSrc = document.currentScript.src;
    let templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
    $.get(templateUrl, template => {
        component.template = template;
        retry(() => {
            if (!window.vue) // if vue is not ready, do a retry
                return false;
            window.vue.component(name, component);
        })
    });
}

function setRoomSecuritySystemEnabled(roomName, isEnabled) {
    setRoom(roomName, { IsSecuritySystemEnabled: isEnabled })
        .done(() => {
            let parent = this.$parent;
            if (parent)
                getRooms().done(rooms => parent.rooms = rooms)
        });
}

function filterObjectBySettings(object, settings) {
    return Object.keys(object)
        .filter(key => key[0] == "$" || settings == null || object['$subtypes'][key].setting == settings)
        .reduce((cur, key) => {
            let value = object[key] && object[key]['$subtypes'] ? filterObjectBySettings(object[key]) : object[key];
            return Object.assign(cur, { [key]: value })
        }, {});
}


function createLineChart(canvasId, allowZoom = true) {
    let cfg = {
        type: 'line',
        data: {
            datasets: []
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
                    mode: "nearest",
                    axis: "x"
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

function setChartData(chart, label, data, type) { // data: {x: date, y: value}
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
            fill: true,
            type: type
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

function accumulateData(data, groupCallback, sum) {
    let groups = Object.groupBy(data, groupCallback);
    let result = [];
    for (let key of Object.keys(groups)) {
        let value = groups[key].reduce((sum, curr) => sum + curr.y, 0.0);
        if (!sum)
            value /= groups[key].length;
        result.push({ x: new Date(key), y: Math.round(value * 100) / 100});
    }
    return result.sort((a, b) => (a.x > b.x) ? 1 : -1);
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
    if (typeof date == "string")
        date = new Date(date);

    let time = `${date.getHours()}:`.padStart(3, '0') + `${date.getMinutes()}:`.padStart(3, '0') + `${date.getSeconds()}`.padStart(2, '0');
    if (date.toDateString() == new Date().toDateString()) // it's today return only time
        return time;

    return `${date.getDate()}/`.padStart(3, '0') + `${date.getMonth() + 1}/`.padStart(3, '0') + `${date.getFullYear()} ` + time;
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

function retry(func, retries = 3, delay = 100) {
    if (retries <= 0)
        return;

    try {
        if (func() == false)
            throw new Error();
    } catch (err) {
        console.debug(`Action retry failed ${err}`)
        setTimeout(() => retry(func, retries - 1, delay), delay);
    }
}