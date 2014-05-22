
/**
* @ngdoc directive
* @name umbraco.directives.directive:valTab
* @restrict A
* @description Used to show validation warnings for a tab to indicate that the tab content has validations errors in its data.
* In order for this directive to work, the valFormManager directive must be placed on the containing form.
**/
function valTab() {
    return {
        //this means the form is optional and will not throw an exception if not found
        // if the form is not found, then this directive will not do anything
        require: "^?form", 
        restrict: "A",
        link: function (scope, element, attr, formCtrl) {
            
            if (formCtrl) {
                var tabId = "tab" + scope.tab.id;

                scope.tabHasError = false;

                //listen for form validation changes
                scope.$on("valStatusChanged", function (evt, args) {
                    if (!args.form.$valid) {
                        var tabContent = element.closest(".umb-panel").find("#" + tabId);
                        //check if the validation messages are contained inside of this tabs 
                        if (tabContent.find(".ng-invalid").length > 0) {
                            scope.tabHasError = true;
                        } else {
                            scope.tabHasError = false;
                        }
                    }
                    else {
                        scope.tabHasError = false;
                    }
                });
            }

        }
    };
}
angular.module('umbraco.directives').directive("valTab", valTab);