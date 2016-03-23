(function() {
    'use strict';

    function EditorHeaderDirective(iconHelper, variationsHelper) {

        function link(scope, el, attr, ctrl) {

            scope.showVariationsQuickSwitch = false;
            scope.variations = variationsHelper.getVariations();

            scope.openIconPicker = function() {
                scope.dialogModel = {
                    view: "iconpicker",
                    show: true,
                    submit: function(model) {
                        if (model.color) {
                            scope.icon = model.icon + " " + model.color;
                        } else {
                            scope.icon = model.icon;
                        }

                        // set form to dirty
                        ctrl.$setDirty();

                        scope.dialogModel.show = false;
                        scope.dialogModel = null;
                    }
                };
            };

            scope.toggleVariationsQuickSwitch = function() {
                scope.showVariationsQuickSwitch = !scope.showVariationsQuickSwitch;
            };

            scope.hideVariationsQuickSwitch = function() {
                scope.showVariationsQuickSwitch = false;
            };

            scope.toggleSubItems = function(item, $event) {
                item.showSubItems = !item.showSubItems;
                $event.preventDefault();
                $event.stopPropagation();

            };

            scope.selectItem = function(language, variation) {

                if(language) {
                    scope.language = language.language;
                } else {
                    scope.language = "";
                }

                if(variation) {
                    scope.variation = variation.name;
                } else {
                    scope.variation = "";
                }
                
                scope.showVariationsQuickSwitch = false;
            };

        }

        var directive = {
            require: '^form',
            transclude: true,
            restrict: 'E',
            replace: true,
            templateUrl: 'views/components/editor/umb-editor-header.html',
            scope: {
                tabs: "=",
                actions: "=",
                name: "=",
                nameLocked: "=",
                menu: "=",
                icon: "=",
                hideIcon: "@",
                alias: "=",
                hideAlias: "@",
                description: "=",
                hideDescription: "@",
                navigation: "="
            },
            link: link
        };

        return directive;
    }

    angular.module('umbraco.directives').directive('umbEditorHeader', EditorHeaderDirective);

})();
