var Ldv = window.Ldv || {};

Ldv.CloneButton_PopulateAppDropdownMenu = function(commandProperties, entityName, isView)
{
	try
	{
		var result =
			$.ajax({
				type: "GET",
				contentType: "application/json; charset=utf-8",
				datatype: "json",
				url: Xrm.Page.context.getClientUrl() + "/api/data/v8.2/ldv_clonerecordtemplates?"
					+ "$select=ldv_clonerecordtemplateid,ldv_name"
					+ "&$filter=ldv_targetentity eq '" + entityName + "' and  statecode eq 0",
				beforeSend: function(xmlHttpRequest)
				{
					xmlHttpRequest.setRequestHeader("OData-MaxVersion", "4.0");
					xmlHttpRequest.setRequestHeader("OData-Version", "4.0");
					xmlHttpRequest.setRequestHeader("Accept", "application/json");
				},
				async: false
			});

		var menu =
			'<Menu Id="ldv.ApplicationRibbon.{!EntityLogicalName}.Clone.' + (isView ? 'View' : 'Entity') + '.Button.Menu">'
				+ '  <MenuSection Id="ldv.ApplicationRibbon.{!EntityLogicalName}.Clone.' + (isView ? 'View' : 'Entity') + 'Menu.Section" Sequence="5" DisplayMode="Menu16">'
				+ '    <Controls Id="ldv.ApplicationRibbon.{!EntityLogicalName}.Clone.' + (isView ? 'View' : 'Entity') + 'Menu.Section.Controls">';

		result = result.responseJSON.value;

		for (var i = 0; i < result.length; i++)
		{
			var id = result[i]["ldv_clonerecordtemplateid"];
			var name = result[i]["ldv_name"];

			menu +=
				'      <Button Command="ldv.ApplicationRibbon.Clone.' + (isView ? 'View' : 'Entity') + 'PopButton.Command" '
				+ '        Id="' + id + '" '
				+ '        LabelText="' + name + '" '
				+ '        Sequence="' + ((i + 1) * 10) + '" />';
		}

		menu +=
			'      </Controls>'
			+ '  </MenuSection>'
			+ '</Menu>';

		commandProperties["PopulationXML"] = menu;
	}
	catch (e)
	{
		console.error('CloneButton_PopulateAppDropdownMenu');
		console.error(e);
	}
};

Ldv.DefaultCloneTemplateId = null;

Ldv.CloneViewButton_EnableRule = function(primaryEntityTypeName, selectedRefs)
{
	if (!Ldv.DefaultCloneTemplateId)
	{
		Ldv.CloneRetrieveDefaultTemplate(primaryEntityTypeName);
	}

	return Ldv.DefaultCloneTemplateId != null && selectedRefs.length > 0;
};

Ldv.CloneEntityButton_EnableRule = function(primaryEntityTypeName)
{
	if (!Ldv.DefaultCloneTemplateId)
	{
		Ldv.CloneRetrieveDefaultTemplate(primaryEntityTypeName);
	}

	return Ldv.DefaultCloneTemplateId != null;
};

Ldv.CloneRetrieveDefaultTemplate = function(primaryEntityTypeName)
{
	try
	{
		var result =
			$.ajax({
				type: "GET",
				contentType: "application/json; charset=utf-8",
				datatype: "json",
				url: Xrm.Page.context.getClientUrl()
					+ "/api/data/v8.2/ldv_clonerecordtemplates"
					+ "?$select=ldv_clonerecordtemplateid"
					+ "&$filter=ldv_targetentity eq '" + primaryEntityTypeName + "' and  statecode eq 0"
					+ "&$orderby=ldv_isdefault desc",
				beforeSend: function(xmlHttpRequest)
				{
					xmlHttpRequest.setRequestHeader("OData-MaxVersion", "4.0");
					xmlHttpRequest.setRequestHeader("OData-Version", "4.0");
					xmlHttpRequest.setRequestHeader("Accept", "application/json");
					xmlHttpRequest.setRequestHeader("Prefer", "odata.maxpagesize=1");
				},
				async: false
			});

		var results = result.responseJSON.value;

		if (results.length)
		{
			Ldv.DefaultCloneTemplateId = results[0]["ldv_clonerecordtemplateid"];
		}
	}
	catch (e)
	{
		console.error('CloneButton_PopulateAppDropdownMenu');
		console.error(e);
	}
};

Ldv.CloneViewButton_OnDefaultClick = function(selectedRefs)
{
	if (selectedRefs.length)
	{
		Ldv.CallCloneIncrement(selectedRefs, 0, Ldv.DefaultCloneTemplateId);
	}
};

Ldv.CloneEntityButton_OnDefaultClick = function(primaryEntityTypeName, firstPrimaryItemId)
{
	var entityName = primaryEntityTypeName;
	var entityId = firstPrimaryItemId.replace(/[{}]/gi, '');

	Ldv.CloneRecord(Ldv.DefaultCloneTemplateId, entityName, entityId, null, null, true);
};

Ldv.CloneEntityButton_OnCloneClick = function(commandProperties, primaryEntityTypeName, firstPrimaryItemId)
{
	var templateId = commandProperties.SourceControlId;
	var entityName = primaryEntityTypeName;
	var entityId = firstPrimaryItemId.replace(/[{}]/gi, '');

	Ldv.CloneRecord(templateId, entityName, entityId, null, null, true);
};

Ldv.CloneViewButton_OnCloneClick = function(commandProperties, selectedRefs)
{
	var templateId = commandProperties.SourceControlId;

	if (selectedRefs.length)
	{
		Ldv.CallCloneIncrement(selectedRefs, 0, templateId);
	}
};

Ldv.CallCloneIncrement = function(selectedRefs, i, templateId)
{
	var ref = selectedRefs[i];
	var entityName = ref.TypeName;
	var entityId = ref.Id.replace(/[{}]/gi, '');

	Ldv.CloneRecord(templateId, entityName, entityId,
		function()
			{
				if (i < selectedRefs.length - 1)
				{
					Ldv.CallCloneIncrement(selectedRefs, i + 1, templateId);
				}
			},
		'"' + ref.Name + '" (' + (i + 1) + '/' + selectedRefs.length + ')',
		selectedRefs.length === 1);
};

Ldv.CloneRecord = function(templateId, entityName, entityId, callback, message, isGoToRecord)
{
	ShowBusyIndicator('Copying record ' + (message || '') + ' ... ', 'cloneRecordByTemplateCopy' + entityId);

	var parameters = {};
	parameters.EntityName = entityName;
	parameters.EntityId = entityId;

	$.ajax({
		type: "POST",
		contentType: "application/json; charset=utf-8",
		datatype: "json",
		url: Xrm.Page.context.getClientUrl() + "/api/data/v8.2/ldv_clonerecordtemplates(" + templateId + ")"
			+ "/Microsoft.Dynamics.CRM.ldv_GenericActionCloneCloneRecordUsingTemplate",
		data: JSON.stringify(parameters),
		beforeSend: function(xmlHttpRequest)
		{
			xmlHttpRequest.setRequestHeader("OData-MaxVersion", "4.0");
			xmlHttpRequest.setRequestHeader("OData-Version", "4.0");
			xmlHttpRequest.setRequestHeader("Accept", "application/json");
		},
		async: true,
		success: function(data, textStatus, xhr)
		{
			HideBusyIndicator('cloneRecordByTemplateCopy' + entityId);

			if (callback)
			{
				callback();
			}

			if (isGoToRecord)
			{
				Xrm.Utility.confirmDialog('Do you want to go to the cloned record?',
					function()
						{
							setTimeout(
								function()
									{
										GoToRecord(entityName, data.ClonedRecordId);
									}, 200);
						});
				return;
			}
		},
		error: function(xhr, textStatus, errorThrown)
		{
			HideBusyIndicator('cloneRecordByTemplateCopy' + entityId);
			console.error('OnCloneClick => trigger action');
			console.error(xhr);
			console.error(textStatus);
			console.error(errorThrown);
			Xrm.Utility.alertDialog('Clone operation failed.');
		}
	});
};

//#region Helpers

function LoadWebResources(resources, callback, scopeWindow)
{
	/// <summary>
	///     Takes an array of resource names and loads them into the current context using "LoadScript".<br />
	///     The resources param accepts a string as well in case a single resource is needed instead.<br />
	///     Author: Ahmed el-Sawalhy
	/// </summary>
	/// <param name="resources" type="String[] | string" optional="false">The resource[s] to load.</param>
	/// <param name="callback" type="Function" optional="true">A function to call after resource[s] has been loaded.</param>
	try
	{
		if (resources.length <= 0)
		{
			if (callback)
			{
				callback();
			}

			return;
		}

		if (typeof resources === 'string')
		{
			resources = [resources];
		}

		var localCallback = function()
		{
			try
			{
				if (resources.length > 1)
				{
					LoadWebResources(resources.slice(1, resources.length), callback, scopeWindow);
				}
				else
				{
					if (callback)
					{
						callback();
					}
				}
			}
			catch (e)
			{
				console.error('Clone => LoadWebResources => localCallback');
				console.error(e);
			}
		};

		LoadScript(Xrm.Page.context.getClientUrl() + '/WebResources/' + resources[0], localCallback, scopeWindow);
	}
	catch (e)
	{
		console.error('Clone => LoadWebResources');
		console.error(e);
	}
}

function LoadScript(url, callback, scopeWindow)
{
	/// <summary>
	///     Takes a URL of a script file and loads it into the current context, and then calls the function passed.<br />
	///     Author: Ahmed el-Sawalhy<br />
	///     credit: http://stackoverflow.com/a/950146/1919456
	/// </summary>
	/// <param name="url" type="String" optional="false">The URL to the script file.</param>
	/// <param name="callback" type="Function" optional="true">The function to call after loading the script.</param>

	// Adding the script tag to the head as suggested before
	try
	{
		scopeWindow = scopeWindow || window;
		var head = scopeWindow.document.getElementsByTagName('head')[0];
		var script = scopeWindow.document.createElement('script');
		script.type = 'text/javascript';
		script.src = url;

		// Then bind the event to the callback function.
		// There are several events for cross browser compatibility.

		if (callback)
		{
			//script.onreadystatechange = callback;
			script.onload = callback;
		}

		// Fire the loading
		head.appendChild(script);
	}
	catch (e)
	{
		console.error('Clone => LoadScript');
		console.error(e);
	}
}

function LoadWebResourceCss(fileName, scopeWindow)
{
	try
	{
		// modified it to be generic -- Sawalhy
		LoadCss(Xrm.Page.context.getClientUrl() + '/WebResources/' + fileName, scopeWindow);
	}
	catch (e)
	{
		console.error('Clone => LoadWebResourceCss');
		console.error(e);
	}
}

function LoadCss(path, scopeWindow)
{
	try
	{
		scopeWindow = scopeWindow || window;
		var head = scopeWindow.document.getElementsByTagName('head')[0];
		var link = scopeWindow.document.createElement('link');
		link.rel = 'stylesheet';
		link.type = 'text/css';
		link.href = path;
		link.media = 'all';
		head.appendChild(link);
	}
	catch (e)
	{
		console.error('Clone => LoadCss');
		console.error(e);
	}
}

//#endregion

LoadWebResources('ldv_commongenericjs');
