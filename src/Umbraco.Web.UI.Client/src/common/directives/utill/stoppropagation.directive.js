/**
* @ngdoc directive
* @name umbraco.directives.directive:stopPropagation
**/
angular.module("umbraco.directives")
    .directive('stopPropagation', function () {
        return function (scope, element, attrs) {

            var enabled = true;
            //check if there's a value for the attribute, if there is and it's false then we conditionally don't 
            //stop propogation
            if (attrs.preventDefault) {
                attrs.$observe("stopPropagation", function (newVal) {
                    enabled = (newVal === "false" || newVal === 0 || newVal === false) ? false : true;
                });
            }

            $(element).click(function (event) {
                if (event.metaKey || event.ctrlKey) {
                    return;
                }
                else {
                    if (enabled === true) {
                        event.stopPropagation();
                    }
                }
            });
        };
    }); 