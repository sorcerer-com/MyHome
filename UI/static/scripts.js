function request(method, url, func, postData) {
	if (!window.XMLHttpRequest)
		return;

	var xhttp = new XMLHttpRequest();
	xhttp.timeout = 4000; // Set timeout to 4 seconds (4000 milliseconds)
	xhttp.onreadystatechange = func;
	xhttp.open(method, url, true);
	xhttp.setRequestHeader("Content-type", "application/x-www-form-urlencoded");
	xhttp.send(postData);
}

function submitForm(formId) {
	document.getElementById(formId).submit()
}

function resetForm(formId) {
	document.getElementById(formId).reset()
}


function setText(formId, inputName, text) {
	var form = document.getElementById(formId);
	var element = form.elements[inputName];
	element.value = text;
}

function addText(formId, inputName, text) {
	var form = document.getElementById(formId);
	var element = form.elements[inputName];
	element.value += text;
}

function removeText(formId, inputName, count) {
	var form = document.getElementById(formId);
	var element = form.elements[inputName];
	element.value = element.value.substr(0, element.value.length - count);
}


function addItem(parent, type, name, value, button = false, line = false) {
	if (type != "bool") {
		var element = document.createElement("input");
		// TODO: implement other types
		//console.log(name + ": " + type);
		if (type == "int")
			element.type = "number";
		else
			element.type = "text";
		element.value = value;
		element.title = value;
	} else {
		var element = document.createElement("select");
		for (v of ["True", "False"]) {
			var option = document.createElement("option");
			option.value = v;
			option.label = v;
			if (value == v)
				option.selected = true;
			element.appendChild(option);
		}
	}
	element.name = name;
	parent.appendChild(element);

	if (button) {
		parent.appendChild(document.createTextNode("\n"));
		var button = document.createElement("a");
		button.className = "button";
		button.href = "javascript:;";
		button.onclick = function () { removeItem(button); };
		button.text = "-";
		parent.appendChild(button);
	}

	if (line) {
		parent.append(document.createElement("hr"));
	}
}

function removeItem(sender, level = 1) {
	var parent = sender;
	for (var i = 0; i < level; i++)
		parent = parent.parentElement;
	parent.parentElement.removeChild(parent);
}


function toggleDetails(sender) {
	var details = sender.parentElement.getElementsByTagName("details");
	for (var i = 0; i < details.length; i++)
		if (details[i] != sender)
			details[i].removeAttribute("open");
}

function toggleCollapse(sender) {
	var parent = sender.parentElement;
	var idx = Array.prototype.indexOf.call(parent.children, sender);
	if (parent.children[idx + 1].style.display == "" || parent.children[idx + 1].style.display == "block") {
		parent.children[idx - 1].innerHTML = "+";
		parent.children[idx].classList.add("collapsed");
		parent.children[idx].classList.remove("expanded");
		parent.children[idx + 1].style.display = "none";
	}
	else {
		parent.children[idx - 1].innerHTML = "-";
		parent.children[idx].classList.add("expanded");
		parent.children[idx].classList.remove("collapsed");
		parent.children[idx + 1].style.display = "block";
	}
}


function openTab(sender, callbackName) {
	var tabContainer = sender.parentElement;
	var idx = Array.prototype.indexOf.call(tabContainer.children, sender);

	var tabButtons = tabContainer.getElementsByTagName("button");
	for (var i = 0; i < tabButtons.length; i++) {
		tabButtons[i].className = tabButtons[i].className.replace("active", "");
	}
	sender.className += " active";

	var tabContents = tabContainer.getElementsByClassName("tabContent");
	for (var i = 0; i < tabContents.length; i++) {
		tabContents[i].className = tabContents[i].className.replace(" active", "");
		if (i == idx)
			tabContents[i].className += " active";
	}

	callIfExists(callbackName, tabContents[idx]);
}

function callIfExists(func, sender) {
	if (window[func] == undefined)
		return;
	window[func](sender);
}


function drawLineChart(canvasId, values, names, drawValues, drawAxis) {
	var axisStyle = '#000000';
	var axisGuideStyle = 'rgba(255, 255, 255, 0.3)';
	var chartLineStyle = '#0000cc';
	var charUnderLineStyle = 'rgba(128, 128, 128, 0.3)';
	var textStyle = '#666666';
	if (!Array.isArray(values) || values.length == 0)
		return

	var minValue = values[0], maxValue = values[0];
	for (i = 1; i < values.length; i++) {
		minValue = Math.min(minValue, values[i]);
		maxValue = Math.max(maxValue, values[i]);
	}
	var delta = Math.max(maxValue - minValue, 0.01);

	var canvas = document.getElementById(canvasId);
	var context = canvas.getContext("2d");
	var withEps = (canvas.width - 20) / (values.length - 1);
	var heightEps = (canvas.height - 40) / delta;
	var start = { x: 10, y: canvas.height - 20 };

	if (drawAxis) {
		var maxValueWidth = context.measureText(maxValue).width;
		var textHeight = parseInt(context.font);
		// draw X and Y axis
		context.beginPath();
		context.moveTo(maxValueWidth + 10, 10);
		context.lineTo(maxValueWidth + 10, canvas.height - 10);
		context.moveTo(maxValueWidth + 10, canvas.height - 10);
		context.lineTo(canvas.width - 10, canvas.height - 10);
		context.strokeStyle = axisStyle;
		context.stroke();

		for (i = Math.round(minValue); i <= maxValue; i++) {
			if (i % Math.max(Math.round((textHeight + 2) / heightEps), 1) != 0)
				continue;
			// draw horizontal line
			context.beginPath();
			context.strokeStyle = axisGuideStyle;
			context.moveTo(maxValueWidth + 10 + 1, start.y - (i - minValue) * heightEps);
			context.lineTo(canvas.width - 10 + 1, start.y - (i - minValue) * heightEps);
			context.stroke();
			// draw value
			var textWidth = context.measureText(i).width;
			context.strokeStyle = textStyle;
			context.strokeText(i, maxValueWidth + 5 - textWidth, start.y - (i - minValue) * heightEps + textHeight / 2);
		}

		withEps = (canvas.width - 20 - maxValueWidth - 15) / (values.length - 1);
		start = { x: 10 + maxValueWidth + 10, y: canvas.height - 20 };
	}

	// fill area below the curve
	context.beginPath();
	context.fillStyle = charUnderLineStyle;
	context.moveTo(start.x, start.y);
	context.lineTo(start.x, start.y - (values[0] - minValue) * heightEps);
	for (i = 1; i < values.length; i++) {
		var pos = { x: start.x + i * withEps, y: start.y - (values[i] - minValue) * heightEps };
		context.lineTo(pos.x, pos.y);
	}
	context.lineTo(start.x + (values.length - 1) * withEps, start.y);
	context.fill();

	// draw the curve
	context.beginPath();
	context.strokeStyle = chartLineStyle;
	context.fillStyle = chartLineStyle;
	context.moveTo(start.x, start.y - (values[0] - minValue) * heightEps);
	context.fillRect(start.x - 2, start.y - (values[0] - minValue) * heightEps - 2, 4, 4);
	for (i = 1; i < values.length; i++) {
		var pos = { x: start.x + i * withEps, y: start.y - (values[i] - minValue) * heightEps };
		context.lineTo(pos.x, pos.y);
		context.fillRect(pos.x - 2, pos.y - 2, 4, 4);
	}
	context.stroke();

	if (drawValues) {
		context.strokeStyle = textStyle;
		var prevValue = minValue - delta; // to be sure that first value will be shown
		for (var i = 0; i < values.length; i++) {
			if (i > 0 && Math.abs(values[i] - prevValue) < delta * 0.1)
				continue;
			prevValue = values[i];
			var pos = { x: start.x + i * withEps, y: start.y - (values[i] - minValue) * heightEps };
			var textWidth = context.measureText(values[i]).width;
			var textHeight = parseInt(context.font);
			if (i > 0 && values[i - 1] > values[i]) textHeight *= -1;
			else if (i > 0 && values[i - 1] == values[i] && i % 2 == 0) textHeight *= -1;
			context.strokeText(values[i], pos.x - textWidth / 2, pos.y + 4 - textHeight);
		}
	}

	var tooltip = document.getElementById("tooltip");
	if (Array.isArray(names) && tooltip != undefined) {
		canvas.onmousemove = function (e) {
			tooltip.style.visibility = "collapse";
			var canvasPosX = e.clientX - canvas.getBoundingClientRect().left;
			var canvasPosY = (e.clientY - canvas.getBoundingClientRect().top);
			var idx = Math.round((canvasPosX - start.x) / withEps);
			if (idx < 0 || idx >= values.length)
				return;
			// Show hint not only over point but the whole vertical
			//var chartY = start.y - (values[idx] - minValue) * heightEps;
			//if (Math.abs(chartY - canvasPosY) > 5)
			//	return;

			tooltip.style.left = e.pageX + "px";
			tooltip.style.top = (e.pageY + 20) + "px";
			tooltip.style.visibility = "visible";
			tooltip.innerHTML = names[idx] + ": " + values[idx];
		};
		canvas.onmouseout = function (e) {
			tooltip.style.visibility = "collapse";
		}
	}
}