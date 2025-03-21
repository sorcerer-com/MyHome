﻿setComponent("main-page", {
    data: function () {
        return {
            rooms: [],
            security: {},
            assistant: {},

            assistantRequest: '',
            assistantChatShown: false,
            assistantRecord: null,

            modal: "",
            modalSelection: "",
            stats: {}
        }
    },
    computed: {
        showMap: function () {
            return this.$route.path == "/map";
        },
        notifications: function () {
            return this.$root.notifications.filter(n => n.isValid);
        },
        allRoomsSecuritySystemEnabled: function () {
            return this.rooms.length > 0 && this.rooms.every(r => r.IsSecuritySystemEnabled)
        },
        someRoomSecuritySystemEnabled: function () {
            return this.rooms.some(r => r.IsSecuritySystemEnabled) && !this.rooms.every(r => r.IsSecuritySystemEnabled)
        },
        chatHistory: function () {
            return this.assistant.History ? [...this.assistant.History].reverse() : [];
        }
    },
    methods: {
        dateToString: dateToString,
        notificationAction: function (notification) {
            switch (notification.message) {
                case "System upgrade": return "Upgrade";
                case "Backup Mode": return "";
                case "Discovered device/s": return "See";
                default: return "Dismiss";
            }
        },

        refreshData: function () {
            getRooms().done(rooms => {
                this.rooms = rooms;
                // update security modal if opened
                if (this.modal == "Security")
                    this.showSecurityModal();
            });

            getSystem("Security").done(security => this.security = security);
            getSystem("Assistant").done(assistant => {
                this.assistant = assistant;
                if (this.assistantChatShown)
                    this.assistant.UnreadHistoryItems = 0;
            });

            if (!window.ws || window.ws.readyState != WebSocket.OPEN)
                setTimeout(this.refreshData, 3000);
        },

        notificationClick: function (notification) {
            if (notification.message == "System upgrade")
                this.$root.upgrade();
            else if (notification.message == "Discovered device/s")
                this.$router.push("/config#discovered");
            else if (notification.message != "Backup Mode")
                removeNotification(notification.message).done(() => {
                    let idx = this.$root.notifications.indexOf(notification);
                    this.$root.notifications.splice(idx, 1);
                });
        },
        snoozeAlert: function (event) {
            snoozeAlert(event.target.name, event.target.value);
            event.target.value = "";
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

                    setChartData(this.charts["chartSecurity"], room.Name, chartData);

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

                    setChartData(this.charts["chartSecurity"], name, chartData);

                    this.stats[name] = {
                        Average: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) / chartData.length * 100) / 100,
                        Sum: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) * 100) / 100
                    };
                }
            }, 10);
        },

        mouseOverAssistantChat: function () {
            if (this.assistant.UnreadHistoryItems > 0) {
                this.assistant.UnreadHistoryItems = 0;
                setSystem("AssistantSystem", { "UnreadHistoryItems": this.assistant.UnreadHistoryItems });
            }
            this.assistantChatShown = true;
        },
        sendAssistantRequest: function () {
            if (this.assistantRequest == '')
                return;

            this.assistant.History?.push({ "Message": this.assistantRequest, "Time": new Date(), "Response": false });
            this.assistant.History?.push({ "Message": "...", "Response": true });
            callSystem("Assistant", "ProcessRequest", this.assistantRequest)
                .fail(response => this.assistant.History?.splice(0, 0, { "Message": "Error: " + response, "Response": true }));
            this.assistantRequest = '';
        },
        startAssistantRequestRecording: function () {
            let self = this;
            navigator.getUserMedia({ audio: true }, function (stream) {
                self.assistantRecord = RecordRTC(stream, {
                    type: 'audio',
                    mimeType: 'audio/wav',
                    sampleRate: 44100,
                    desiredSampRate: 16000,
                    recorderType: StereoAudioRecorder,
                    numberOfAudioChannels: 1,
                    timeSlice: 5000
                });
                self.assistantRecord.startRecording();
            }, function (error) {
                console.error(JSON.stringify(error));
            });
        },
        stopAssistantRequestRecording: function () {
            let self = this;
            // stop audio recorder
            this.assistantRecord.stopRecording(function () {
                let blob = self.assistantRecord.getBlob();
                self.assistantRecord.reset();
                let formData = new FormData();
                formData.append("data", blob);
                self.assistant.History?.push({ "Message": "...", "Response": false });
                self.assistant.History?.push({ "Message": "...", "Response": true });
                sendRecord(formData)
                    .fail(response => self.assistant.History?.push({ "Message": "Error: " + response, "Response": true }));;
            });
        }
    },
    mounted: function () {
        this.refreshData();
        window.ws?.addRefreshHandlers(this.refreshData);
    },
    unmounted: function () {
        window.ws?.removeRefreshHandlers(this.refreshData);
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
                                setChartData(this.charts["charts"], `${sensor.Name}.${valueType}`, {});
                            }
                            getSensorData(room.Name, sensor.Name, valueType).done(data => {
                                let chartData = Object.keys(data)
                                    .map(k => { return { x: new Date(k), y: data[k] } })
                                    .filter(e => e.x < prevDay)
                                    .sort((a, b) => (a.x > b.x) ? 1 : -1);
                                if (this.charts["charts"].data.datasets.some(ds => ds.label == `${sensor.Name}.${valueType}`)) // ensure that this call isn't old
                                    setChartData(this.charts["charts"], `${sensor.Name}.${valueType}`, chartData);

                                if (`${sensor.Name}.${valueType}` in this.stats) {
                                    this.stats[`${sensor.Name}.${valueType}`] = {
                                        Average: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) / chartData.length * 100) / 100,
                                        Sum: Math.round(chartData.reduce((sum, curr) => sum + curr.y, 0) * 100) / 100
                                    };
                                }
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