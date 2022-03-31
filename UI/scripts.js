function setRoomSecuritySystemEnabled(roomName, isEnabled) {
    setRoom(roomName, { IsSecuritySystemEnabled: isEnabled })
        .done(() => getRooms().done(rooms => Vue.set(this.$parent, "rooms", rooms)));
}


function showLineChart(canvas, data, label) {
    let cfg = {
        data: {
            datasets: [{
                label: label,
                backgroundColor: Chart.helpers.color("blue").alpha(0.1).rgbString(),
                borderColor: Chart.helpers.color("blue").alpha(0.5).rgbString(),
                data: data,
                type: "line",
                lineTension: 0,
                pointRadius: 0,
                borderWidth: 2
            }]
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
            }
        }
    };
    return new Chart(canvas, cfg);
}

function updateChartData(chart, data) {
    chart.data.datasets[0].data = data;
    chart.update();
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