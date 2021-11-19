/**
 * @license Copyright (c) 2003-2021, CKSource - Frederico Knabben. All rights reserved.
 * For licensing, see https://ckeditor.com/legal/ckeditor-oss-license
 */

CKEDITOR.editorConfig = function( config ) {
	// Define changes to default configuration here. For example:
	// config.language = 'fr';
	// config.uiColor = '#AADC6E';
	config.toolbarGroups = [
		{ name: 'basicstyles', groups: ['basicstyles', 'cleanup'] },
		{ name: 'colors', groups: ['colors'] },
		{ name: 'paragraph', groups: ['list', 'indent', 'align', 'bidi', 'blocks', 'paragraph'] },
		{ name: 'editing', groups: ['find', 'selection', 'spellchecker', 'editing'] },
		'/',
		{ name: 'styles', groups: ['styles'] },
		{ name: 'clipboard', groups: ['undo', 'clipboard'] },
		{ name: 'document', groups: ['document', 'doctools', 'mode'] },
		'/',
		{ name: 'links', groups: ['links'] },
		{ name: 'insert', groups: ['insert'] },
		{ name: 'forms', groups: ['forms'] },
		{ name: 'tools', groups: ['tools'] },
		{ name: 'emoji', groups: ['emoji'] },
		{ name: 'about', groups: ['about'] },
		{ name: 'others', groups: ['others'] }
	];
	config.language_list = ['ar:Arabic:rtl'];
	config.editorplaceholder = 'Start typing …';
	config.autoGrow_onStartup = true;
	config.allowedContent = true;
	config.codeSnippet_theme = 'vs';
	config.autoGrow_minHeight = 100;
	config.extraPlugins = "base64image";
};
