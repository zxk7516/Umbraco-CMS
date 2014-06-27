angular.module("umbraco.directives")
.directive('umbBody', function(){
    return {
        restrict: 'E',
        replace: true,
        transclude: 'true',
        templateUrl: 'views/directives/html/umb-body.html'
    };
});