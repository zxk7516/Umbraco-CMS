angular.module("umbraco")
    .directive("umbGridEvents",
        function() {
            return {
                restrict: 'A',
                replace: false,
                link: function(scope, el, attrs) {
                    scope.$el = el;
                }
            };
        });
