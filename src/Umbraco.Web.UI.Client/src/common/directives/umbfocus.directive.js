angular.module("umbraco.directives")
    .directive('umbFocus', ['$parse', function($parse) {
        return function(scope, element, attr) {

            var fn = $parse(attr['umbFocus']);

            element.bind('focus', function(event) {
                scope.$apply(function() {
                    fn(scope, {$event:event});
                });
            });

        };
    }]);