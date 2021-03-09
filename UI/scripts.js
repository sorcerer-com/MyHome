function updateVue(data) {
	if (!window.vue) {
		window.vue = new Vue({
			el: "#vue-content",
			data: data
		});
	} else {
		for (let key in data) {
			Vue.set(window.vue, key, data[key]);
        }
    }
}

function addTextToForm(formId, inputName, text) {
	var form = document.getElementById(formId);
	var element = form.elements[inputName];
	element.value += text;
}

function removeTextFromForm(formId, inputName, count) {
	var form = document.getElementById(formId);
	var element = form.elements[inputName];
	element.value = element.value.substr(0, element.value.length - count);
}

function submitForm(formId) {
	document.getElementById(formId).submit()
}