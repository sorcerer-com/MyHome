var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("monaco-editor", {
        template: template,
        props: ["value"],
        data: function () {
            return {
                editor: null
            }
        },
        methods: {
        },
        mounted: function () {
            getTypescriptModels().done(models => {
                monaco.languages.typescript.javascriptDefaults.setEagerModelSync(true);
                monaco.editor.getModels().forEach(model => model.dispose()); // clear the models
                monaco.editor.createModel(models, "typescript");

                // create editor
                var h_div = document.getElementById('monaco_editor');
                this.editor = monaco.editor.create(h_div, {
                    value: this.value,
                    language: 'typescript',
                    contextmenu: false,
                    lineNumbers: true,
                    lineNumbersMinChars: 3,
                    lineDecorationsWidth: 0,
                    minimap: { enabled: false }
                });
                monaco.languages.typescript.typescriptDefaults.setCompilerOptions(
                    {
                        noLib: true,
                        allowNonTsExtensions: true
                    });
                this.editor.getModel().updateOptions({ tabSize: 2 });
                this.editor.getModel().onDidChangeContent(_ => {
                    // to work the v-model binding
                    this.$emit("input", this.editor.getModel().getValue(monaco.editor.EndOfLinePreference.LF));
                });
            });
        }
    });
});