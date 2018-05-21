function RowConfigController($scope, contentTypeResource, editorService) {

    var vm = this;

    $scope.currentRow = $scope.model.currentRow;
    $scope.contentTypes = $scope.model.contentTypes;
    $scope.columns = $scope.model.columns;

    vm.currentCellDisplayGridEditors = [];

    vm.selectGridEditor = selectGridEditor;
    vm.removeGridEditor = removeGridEditor;
    vm.submit = submit;
    vm.close = close;

    $scope.scaleUp = function(section, max, overflow) {
        var add = 1;
        if (overflow !== true) {
            add = (max > 1) ? 1 : max;
        }
        //var add = (max > 1) ? 1 : max;
        section.grid = section.grid + add;
    };

    $scope.scaleDown = function(section) {
        var remove = (section.grid > 1) ? 1 : 0;
        section.grid = section.grid - remove;
    };

    $scope.percentage = function(spans) {
        return ((spans / $scope.columns) * 100).toFixed(8);
    };

    $scope.toggleCollection = function(collection, toggle) {
        if (toggle) {
            collection = [];
        }
        else {
            delete collection;
        }
    };


    /****************
        area
    *****************/
    $scope.configureCell = function(cell, row) {
        if ($scope.currentCell && $scope.currentCell === cell) {
            delete $scope.currentCell;
        }
        else {
            if (cell === undefined) {
                var available = $scope.availableRowSpace;
                var space = 4;

                if (available < 4 && available > 0) {
                    space = available;
                }

                cell = {
                    grid: space
                };

                row.areas.push(cell);
            }
            $scope.currentCell = cell;
        }
    };

    $scope.deleteArea = function (cell, row) {
    	if ($scope.currentCell === cell) {
    		$scope.currentCell = undefined;
    	}
    	var index = row.areas.indexOf(cell)
    	row.areas.splice(index, 1);
    };

    $scope.closeArea = function() {
        $scope.currentCell = undefined;
    };

    $scope.nameChanged = false;
    var originalName = $scope.currentRow.name;
    $scope.$watch("currentRow", function(row) {
        if (row) {

            var total = 0;
            _.forEach(row.areas, function(area) {
                total = (total + area.grid);
            });

            $scope.availableRowSpace = $scope.columns - total;

            if (originalName) {
                if (originalName != row.name) {
                    $scope.nameChanged = true;
                }
                else {
                    $scope.nameChanged = false;
                }
            }
        }
    }, true);

    function selectGridEditor() {
        var gridEditorPicker = {
            view: "views/propertyEditors/grid2/dialogs/grideditorpicker.html",
            size: "small",
            submit: function(model) {
                
                if(model && model.selection.length > 0) {

                    if(!$scope.currentCell.allowed) {
                        $scope.currentCell.allowed = [];
                    }

                    model.selection.forEach(function(gridEditor){
                        if($scope.currentCell.allowed.indexOf(gridEditor.udi) === -1) {
                            $scope.currentCell.allowed.push(gridEditor.udi);
                        }
                    });
                }

                editorService.close();

            },
            close: function() {
                editorService.close();
            }
        };

        editorService.open(gridEditorPicker);
    }

    function removeGridEditor(index, allowedGridEditors) {
        allowedGridEditors.splice(index, 1);
    }

    function submit(model) {
        if($scope.model.submit) {
            $scope.model.submit();
        }
    }

    function close() {
        if($scope.model.close) {
            $scope.model.close();
        }
    }

}

angular.module("umbraco").controller("Umbraco.PropertyEditors.Grid2PrevalueEditor.RowConfigController", RowConfigController);
