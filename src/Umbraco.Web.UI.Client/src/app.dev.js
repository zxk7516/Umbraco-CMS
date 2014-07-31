var app = angular.module('umbraco', [
	'umbraco.filters',
	'umbraco.directives',
	'umbraco.resources',
	'umbraco.services',
	'umbraco.httpbackend',

	'ngRoute',
    'ngCookies',
    'ngTouch',
    'ngSanitize',
    'ngAnimate',
    'blueimp.fileupload'
]);

/* For Angular 1.2: we need to load in Route, animate and touch seperately
	    'ngRoute',
	    'ngAnimate',
	    'ngTouch'
*/