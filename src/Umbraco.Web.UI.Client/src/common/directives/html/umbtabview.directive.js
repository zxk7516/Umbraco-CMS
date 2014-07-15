/**
* @ngdoc directive
* @name umbraco.directives.directive:umbTabView 
* @restrict E
**/
angular.module("umbraco.directives")
.directive('umbTabView', function($timeout, $log, $parse){
	return {
		restrict: 'E',
		replace: true,
		transclude: 'true',
		templateUrl: 'views/directives/umb-tab-view.html',
		scope: {
			tabs: "="
		}
	};
});