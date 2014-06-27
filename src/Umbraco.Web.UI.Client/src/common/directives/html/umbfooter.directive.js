angular.module("umbraco.directives")
.directive('umbFooter', function(){
    return {
        restrict: 'E',
        replace: true,
        transclude: 'true',
        templateUrl: 'views/directives/html/umb-footer.html'
    };
});