(function() {
    'use strict';

    function EditorHeaderDirective(iconHelper, variationsHelper) {

        function link(scope, el, attr, ctrl) {

            scope.showVariationsQuickSwitchToggle = false;
            scope.showVariationsQuickSwitch = false;

            function activate() {
                showVariationsQuickSwitch(scope.variations);
            }

            function showVariationsQuickSwitch(variations) {
                if(variations && variations.length > 1 || variations && variations.length === 1 && variations[0].variations && variations[0].variations.length > 0) {
                    scope.showVariationsQuickSwitchToggle = true;
                } else {
                    scope.showVariationsQuickSwitchToggle = false;
                }
            }

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

            scope.selectItem = function(item) {
                if(scope.onSelectItem) {
                    
                    for(var i = 0; i < scope.variations.length; i++) {
                        var language = scope.variations[i];
                        language.active = false;

                        if(language.variations && language.variations.length > 0) {
                            for(var variationIndex = 0; variationIndex < language.variations.length; variationIndex++) {
                                var variation = language.variations[variationIndex];
                                variation.active = false;
                            }

                        }
                    }

                    // set selected item to active
                    item.active = true;

                    scope.onSelectItem(item);
                    scope.showVariationsQuickSwitch = false;

                }
            };

            scope.$watch('variations', function(newValue, oldValue){
                showVariationsQuickSwitch(newValue);
            }, true);

            activate();

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
                variation: "=",
                language: "=",
                variations: "=",
                onSelectItem: "=",
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
