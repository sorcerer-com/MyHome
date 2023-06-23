$(window).on("load", () => {
    // wait all components to be loaded
    setTimeout(() => {
        window.vue = new Vue({
            el: "#vue-content",
            data: {
                settings: {},
                logs: [],
                upgradeAvailable: false,

                page: "main",
                modal: "",
                selectedSettings: ""
            },
            computed: {
                isMobile: function () {
                    return /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
                }
            },
            methods: {
                refreshData: function () {
                    // if the page is not visible don't refresh
                    if (document.hidden) {
                        setTimeout(this.refreshData, 1000);
                        return;
                    }

                    getUpgradeAvailable().done(value => {
                        Vue.set(this, "upgradeAvailable", value);
                        setTimeout(this.refreshData, 3000);
                    }).fail(response => {
                        if (response.status == 401) // unauthorized
                            window.location.replace("./login.html");
                        if (response.status == 0) // no connection
                            window.location.reload();
                        setTimeout(this.refreshData, 1000);
                    });

                    // update logs if the modal is opened
                    if (this.modal == "Logs") {
                        getLogs().done(logs => Vue.set(this, "logs", logs.reverse()));
                    }
                },

                showSettingsModal: function () {
                    this.modal = "Settings";
                    this.selectedSettings = "Config";

                    getConfig().done(config => Vue.set(this.settings, "Config", config));
                    getSystems(true).done(systems =>
                        Object.keys(systems).forEach(key => Vue.set(this.settings, key, systems[key])));
                },
                showLogsModal: function () {
                    this.modal = "Logs";
                    getLogs().done(logs => this.logs = logs.reverse());
                },

                saveSettings: function (settings) {
                    if (this.selectedSettings == "Config")
                        return setConfig(settings).done(() => this.modal = "");

                    return setSystem(this.selectedSettings, settings).done(() => this.modal = "");
                },
                upgrade: function () {
                    if (!confirm("Are you sure you want to upgrade the system?"))
                        return;

                    upgrade().done(() => {
                        $("#vue-content").html("Upgrade was successful! Rebooting...");
                        setTimeout(() => window.location.reload(true), 90000);
                    });
                },
                restart: function () {
                    if (!confirm("Are you sure you want to restart the system?"))
                        return;

                    restart().done(() => {
                        $("#vue-content").html("Rebooting...");
                        setTimeout(() => window.location.reload(true), 90000);
                    });
                }
            },
            mounted: function () {
                $("#vue-content").removeClass("w3-hide");
                this.refreshData();
            },
        });
    }, 100);
});