window.vue = Vue.createApp({
    el: "#vue-content",
    data: function () {
        return {
            settings: {},
            logs: [],
            notifications: [],
            restarting: false,

            modal: "",
            selectedSettings: ""
        };
    },
    computed: {
        isMobile: function () {
            return /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
        },
        backupMode: function () {
            return this.notifications.some(n => n.message == "Backup Mode");
        }
    },
    methods: {
        refreshData: function () {
            // if the page is not visible don't refresh
            if (document.hidden) {
                setTimeout(this.refreshData, 1000);
                return;
            }

            getNotifications().done(notifications => {
                this.notifications = notifications;
                setTimeout(this.refreshData, 3000);
            }).fail(response => {
                if (response.status == 401) // unauthorized
                    window.location.replace("./login.html");
                if (response.status == 0 && !this.restarting) // no connection
                    window.location.reload();
                setTimeout(this.refreshData, 1000);
            });

            // update logs if the modal is opened
            if (this.modal == "Logs") {
                getLogs().done(logs => this.logs = logs.reverse());
            }
        },

        showSettingsModal: function () {
            this.modal = "Settings";
            this.selectedSettings = "Config";

            getConfig().done(config => this.settings["Config"] = config);
            getSystems(true).done(systems =>
                Object.keys(systems).forEach(key => this.settings[key] = systems[key]));
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
                setTimeout(() => window.location.reload(true), 180000);
                this.restarting = true;
            }).fail(() => alert("System upgrade failed"));
        },
        restart: function () {
            if (!confirm("Are you sure you want to restart the system?"))
                return;

            restart().done(() => {
                $("#vue-content").html("Rebooting...");
                setTimeout(() => window.location.reload(true), 180000);
                this.restarting = true;
            });
        }
    },
    mounted: function () {
        $("#vue-content").removeClass("w3-hide");
        this.refreshData();
    },
});

$(window).on("load", () => init());

function init() {
    // wait all components to be loaded
    setTimeout(() => {
        // if 'main-page' is not loaded yet
        if (!window.vue._context.components["main-page"]) {
            init();
            return;
        }

        let router = VueRouter.createRouter({
            history: VueRouter.createWebHashHistory(),
            routes: [
                { path: "/", component: window.vue._context.components["main-page"] },
                { path: "/map", component: window.vue._context.components["main-page"] },
                { path: "/config", component: window.vue._context.components["config-page"] },
                { path: "/actions", component: window.vue._context.components["actions-page"] },
                { path: "/assistant", component: window.vue._context.components["assistant-page"] },
                { path: "/:pathMatch(.*)*", redirect: "/" } // not found
            ],
            scrollBehavior(to, from, savedPosition) {
                if (to.hash) {
                    retry(() => {
                        // from "scrollToPosition" function of vue-router.js
                        const el = document.getElementById(to.hash.slice(1));
                        if (!el)
                            return false;
                        const perantRect = el.parentElement.getBoundingClientRect();
                        const elRect = el.getBoundingClientRect();
                        const opt = {
                            behavior: "smooth",
                            left: elRect.left - perantRect.left,
                            top: elRect.top - perantRect.top,
                        };
                        document.getElementById("scrollable-content").scrollTo(opt);
                    }, 5);
                }
            }
        });

        window.vue.use(router)

        window.vue.mount("#vue-content");
    }, 100);
}