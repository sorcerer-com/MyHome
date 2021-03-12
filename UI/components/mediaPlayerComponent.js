var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component('media-player', {
        template: template,
        props: ['media-player'],
        data: function () {
            return {
                selectedMediaItem: ""
            }
        },
        methods: {
            getMediaList: function () {
                if (Object.keys(this.mediaPlayer).length == 0)
                    return {};
                return this.mediaPlayer.MediaList.reduce((pv, x) => {
                    let key = x.substr(0, x.indexOf(":"));
                    let watched = this.mediaPlayer.Watched.includes(x);
                    (pv[key] = pv[key] || []).push({
                        name: x.substr(x.lastIndexOf("\\") + 1),
                        path: x.substr(x.indexOf(":") + 1),
                        watched: watched
                    });
                    return pv;
                }, {});
            },
            selectMediaItem: function (event) {
                this.selectedMediaItem = event.target.title;
                $(event.target).parent().parent().find("a").removeClass("w3-blue-gray"); // unselect all
                $(event.target).addClass("w3-blue-gray");
            },
            callMediaPlayer: function (funcName, ...args) {
                callSystem("MediaPlayer", funcName, ...args)
                    .done(getSystem("MediaPlayer").done(mediaPlayer => updateVue({ mediaPlayer: mediaPlayer })));
            }
        }
    });
});