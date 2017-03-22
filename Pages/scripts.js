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

			
function addText(sender, formId, inputName) {
	var form = document.getElementById(formId);
	var element = form.elements[inputName];
	element.value += sender.text;
}

function removeText(formId, inputName, count) {
	var form = document.getElementById(formId);
	var element = form.elements[inputName];
	element.value = element.value.substr(0, element.value.length - count);
}


function addItem(sender) {
	var parent = sender.parentElement.parentElement;
	var index = Array.prototype.indexOf.call(parent.children, sender.parentElement);
	var newItem = parent.children[index - 1].cloneNode(true);
	parent.insertBefore(newItem, parent.children[index]);
}

function removeItem(sender, level = 1) {
	var parent = sender;
	for (i = 0; i < level; i++)
		parent = parent.parentElement;
	parent.parentElement.removeChild(parent);
}


function toggleDetails(sender) {
	var details = sender.parentElement.getElementsByTagName("details");
	for (var i = 0; i < details.length; i++)
		if (details[i] != sender)
			details[i].removeAttribute("open");
}

			
function drawLineChart(canvasId, values, names, drawValues, drawAxis) {
	var axisStyle = '#000000';
	var axisGuideStyle = 'rgba(255, 255, 255, 0.3)';
	var chartLineStyle = '#0000cc';
	var charUnderLineStyle = 'rgba(128, 128, 128, 0.2)';
	var textStyle = '#666666';
	if (!Array.isArray(values) || values.length == 0)
		return
		
	var minValue = values[0], maxValue = values[0];
	for (i = 1; i < values.length; i++) {
		minValue = Math.min(minValue, values[i]);
		maxValue = Math.max(maxValue, values[i]);
	}
	
	var canvas = document.getElementById(canvasId);
	var context = canvas.getContext("2d");
	var withEps = (canvas.width - 20) / (values.length - 1);
	var heightEps = (canvas.height - 40) / Math.max(maxValue - minValue, 1);
	var start = {x: 10, y: canvas.height - 20};
	
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
		
		for (i = minValue; i <= maxValue; i++) {
			if (i % Math.max(Math.round(textHeight / heightEps), 1) != 0)
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
		start = {x: 10 + maxValueWidth + 10, y: canvas.height - 20};
	}
	
	// fill area below the curve
	context.beginPath();
	context.fillStyle = charUnderLineStyle;
	context.moveTo(start.x, start.y);
	context.lineTo(start.x, start.y - (values[0] - minValue) * heightEps);
	for (i = 1; i < values.length; i++) {
		var pos = {x: start.x + i * withEps, y: start.y - (values[i] - minValue) * heightEps};
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
		var pos = {x: start.x + i * withEps, y: start.y - (values[i] - minValue) * heightEps};
		context.lineTo(pos.x, pos.y);
		context.fillRect(pos.x - 2, pos.y - 2, 4, 4);
	}
	context.stroke();
	
	if (drawValues) {
		context.strokeStyle = textStyle;
		for (i = 0; i < values.length; i++) {
			if (i > 0 && values[i] == values[i - 1])
				continue;
			var pos = {x: start.x + i * withEps, y: start.y - (values[i] - minValue) * heightEps};
			var textWidth = context.measureText(values[i]).width;
			var textHeight = parseInt(context.font);
			if (i > 0 && values[i - 1] > values[i]) textHeight *= -1;
			else if (i > 0 && values[i - 1] == values[i] && i % 2 == 0) textHeight *= -1;
			context.strokeText(values[i], pos.x - textWidth / 2, pos.y + 4 - textHeight);
		}
	}
	
	var tooltip = document.getElementById("tooltip");
	if (Array.isArray(names) && tooltip != undefined) {
		canvas.onmousemove = function(e) {
			tooltip.style.visibility = "collapse";
			var canvasPosX = e.clientX - canvas.getBoundingClientRect().left;
			var canvasPosY = (e.clientY - canvas.getBoundingClientRect().top);
			var idx = Math.round((canvasPosX - start.x) / withEps);
			if (idx < 0 || idx >= values.length)
				return;
			var chartY = start.y - (values[idx] - minValue) * heightEps;
			if (Math.abs(chartY - canvasPosY) > 5)
				return;
				
			tooltip.style.left = e.pageX + "px";
			tooltip.style.top = (e.pageY + 20) + "px";
			tooltip.style.visibility = "visible";
			tooltip.innerHTML = names[idx] + ": " + values[idx];
		};
	}
}