(function() {
    'use strict';

    function EditorHeaderDirective(iconHelper) {

        function link(scope, el, attr, ctrl) {

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

            scope.createVariation = function() {
                alert("fjeiofjeiow");
            };

            scope.showVariations = function() {
                if(scope.onShowVariations) {
                    scope.onShowVariations();
                }
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
                navigation: "=",
                onShowVariations: "="
            },
            link: link
        };

        return directive;
    }

    angular.module('umbraco.directives').directive('umbEditorHeader', EditorHeaderDirective);

})();
