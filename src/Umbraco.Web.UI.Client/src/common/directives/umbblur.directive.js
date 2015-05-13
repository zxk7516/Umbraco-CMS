angular.module("umbraco.directives")
    .directive('umbBlur', ['$parse', function($parse) {
        return function(scope, element, attr) {

            var fn = $parse(attr['umbBlur']);

            element.bind('blur', function(event) {
                scope.$apply(function() {
                    fn(scope, {$event:event});
                });
            });

        };
    }]);