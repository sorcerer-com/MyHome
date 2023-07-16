var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    window.vue.component("tree", {
        template: template,
        props: ["rootEl", "root", "tree"],
        methods: {
            selectItem: function (event) {
                this.$emit("change", event.target.title, event.target);
                $(this.rootEl).find("a").removeClass("w3-blue-gray"); // unselect all
                $(event.target).addClass("w3-blue-gray");
            },
            onChange: function (item, el) {
                this.$emit("change", item, el);
            }
        }
    });
});