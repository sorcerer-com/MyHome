var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("media-tree-item", {
        template:
            `<div>
                <a class="w3-show-block w3-hover-blue-gray padding-4 overflow-wrap-break-word margin-left-8"
                    v-for="(item, key) of tree" v-if="item.path"
                    v-bind:class="{'w3-text-dark-gray': item.watched}"
                    v-bind:title="media + ':' + item.path"
                    v-on:click="selectMediaItem">
                    {{key}}
                </a>
                <details class="margin-left-8"
                    v-for="(item, key) of tree" v-if="!item.path">
                    <summary><span>{{key}}</span></summary>
                    <media-tree-item v-bind:tree="item" v-bind:media="media" v-on:change="onMediaChange"></media-tree-item>
                </details>
            </div>`,
        props: ["tree", "media"],
        methods: {
            selectMediaItem: function (event) {
                this.$emit("change", event.target.title);
                $("#media-list").find("a").removeClass("w3-blue-gray"); // unselect all
                $(event.target).addClass("w3-blue-gray");
            },
            onMediaChange: function (media) {
                this.$emit("change", media);
            }
        }
    });

    Vue.component("media-player", {
        template: template,
        props: ["mediaPlayer"],
        data: function () {
            return {
                selectedMediaItem: ""
            }
        },
        methods: {
            lastIndexOfPathSeparator: function (path) {
                return Math.max(path.lastIndexOf("\\"), path.lastIndexOf("/"));
            },
            getMediaTree: function () {
                if (Object.keys(this.mediaPlayer).length == 0)
                    return {};
                let tree = {};
                for (let media of this.mediaPlayer.MediaList) {
                    let path = media;
                    for (let prefix of this.mediaPlayer.MediaPaths) {
                        path = path.replace(prefix, "");
                    }
                    let split = path.split(/\\|\/|:/).filter(e => e); // split by \\, / and :
                    let currItem = tree;
                    for (let s of split) {
                        if (!(s in currItem))
                            currItem[s] = {};
                        currItem = currItem[s];
                    }
                    currItem["path"] = media.substr(media.indexOf(":") + 1);
                    currItem["watched"] = this.mediaPlayer.Watched.includes(media);
                }
                return tree;
            },
            onMediaChange: function (media) {
                this.selectedMediaItem = media;
            },
            callMediaPlayer: function (funcName, ...args) {
                callSystem("MediaPlayer", funcName, ...args)
                    .done(getSystem("MediaPlayer").done(mediaPlayer => updateVue({ mediaPlayer: mediaPlayer })));
            }
        }
    });
});