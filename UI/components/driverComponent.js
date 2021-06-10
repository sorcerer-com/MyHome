var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("driver", {
        template: template,
        props: ["room", "driver"],
        data: function () {
            return {
                debounceSetColor: debounce(this.setColor, 1000) // set only after no changes in 1 sec
            }
        },
        methods: {
            click: function () {
                if ("IsOn" in this.driver) {
                    this.driver.IsOn = !this.driver.IsOn;
                    setDevice(this.room.Name, this.driver.Name, { "IsOn": this.driver.IsOn });
                }
            },

            setColor: function () {
                setDevice(this.room.Name, this.driver.Name, { "Color": this.driver.Color });
            }
        },
        watch: {
            "driver.Color": function () {
                if (/Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent))
                    this.setColor(); // don't debounce for mobile version
                else
                    this.debounceSetColor();
            }
        }
    });
});