(function() {
    "use strict";

    function GridEditorPicker($scope, gridResource, editorService) {

        var vm = this;

        vm.gridEditors = [];

        vm.submit = submit;
        vm.close = close;
        vm.selectGridEditor = selectGridEditor;

        function onInit() {

            // set default title
            if($scope.model && !$scope.model.title) {
                $scope.model.title = "Select Grid Editors";
            }

            // make sure we have an arrow to push to
            if($scope.model && !$scope.model.selection) {
                $scope.model.selection = [];
            }

            gridResource.getGridContentTypes().then(function(gridEditors){
                vm.gridEditors = gridEditors;
            });
            
        }

        function selectGridEditor(selectedGridEditor) {

            if(!selectedGridEditor) {
                return;
            }

            var index = $scope.model.selection.indexOf(selectedGridEditor);

            // select grid editor
            if(index === -1) {
                selectedGridEditor.selected = true;
                $scope.model.selection.push(selectedGridEditor);
                
            // deselect grid editor    
            } else {
                selectedGridEditor.selected = false;
                $scope.model.selection.splice(index, 1);
            }
            
        }

        function submit(model) {
            if($scope.model.submit) {
                $scope.model.submit(model);
            }  
        }
        
        function close() {
            if($scope.model.close) {
                $scope.model.close();
            }
        }

        onInit();

    }

    angular.module("umbraco").controller("Umbraco.PropertyEditors.GridPrevalueEditor.GridEditorPickerController", GridEditorPicker);

})();