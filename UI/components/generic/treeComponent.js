setComponent("tree", {
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