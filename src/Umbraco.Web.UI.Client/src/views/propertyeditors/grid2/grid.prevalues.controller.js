angular.module("umbraco")
    .controller("Umbraco.PropertyEditors.Grid2PrevalueEditorController",
    function ($scope, $http, assetsService, $rootScope, dialogService, mediaResource, gridService, imageHelper, $timeout, editorService, gridResource) {

        var vm = this;

        var emptyModel = {
            styles:[
                {
                    label: "Set a background image",
                    description: "Set a row background",
                    key: "background-image",
                    view: "imagepicker",
                    modifier: "url({0})"
                }
            ],

            config:[
                {
                    label: "Class",
                    description: "Set a css class",
                    key: "class",
                    view: "textstring"
                }
            ],

            columns: 12,
            templates:[
                {
                    name: "1 column layout",
                    sections: [
                        {
                            grid: 12,
                        }
                    ]
                }
            ],


            layouts:[
                {
                    label: "Headline",
                    name: "Headline",
                    areas: [
                        {
                            grid: 12
                        }
                    ]
                },
                {
                    label: "Article",
                    name: "Article",
                    areas: [
                        {
                            grid: 4
                        },
                        {
                            grid: 8
                        }
                    ]
                }
            ]
        };

        vm.configureRow = configureRow;

        /****************
            Row
        *****************/

        function configureRow(row) {

            var rowCopy = angular.copy($scope.model.value.layouts);

            if(row === undefined){
                row = {
                    name: "",
                    areas: []
                };
                $scope.model.value.layouts.push(row);
            }

            var rowConfigurationEditor = {
                view: "views/propertyEditors/grid2/dialogs/rowconfig.html",
                size: "small",
                currentRow: row,
                columns: $scope.model.value.columns,
                submit: function(model) {
                    editorService.close();
                },
                close: function() {
                    $scope.model.value.layouts = rowCopy;
                    editorService.close();
                }
            }

            editorService.open(rowConfigurationEditor);

        }

        //var rowDeletesPending = false;
        $scope.deleteLayout = function(index) {

           $scope.rowDeleteOverlay = {};
           $scope.rowDeleteOverlay.view = "views/propertyEditors/grid2/dialogs/rowdeleteconfirm.html";
           $scope.rowDeleteOverlay.dialogData = {
             rowName: $scope.model.value.layouts[index].name
           };
           $scope.rowDeleteOverlay.show = true;

           $scope.rowDeleteOverlay.submit = function(model) {

             $scope.model.value.layouts.splice(index, 1);

             $scope.rowDeleteOverlay.show = false;
             $scope.rowDeleteOverlay = null;
           };

           $scope.rowDeleteOverlay.close = function(oldModel) {
             $scope.rowDeleteOverlay.show = false;
             $scope.rowDeleteOverlay = null;
           };

        };


        /****************
            utillities
        *****************/
        $scope.toggleCollection = function(collection, toggle){
            if(toggle){
                collection = [];
            }else{
                delete collection;
            }
        };

        $scope.percentage = function(spans){
            return ((spans / $scope.model.value.columns) * 100).toFixed(8);
        };

        $scope.zeroWidthFilter = function (cell) {
                return cell.grid > 0;
        };

        /****************
            Config
        *****************/

        $scope.removeConfigValue = function(collection, index){
            collection.splice(index, 1);
        };

        var editConfigCollection = function(configValues, title, callback) {

           $scope.editConfigCollectionOverlay = {};
           $scope.editConfigCollectionOverlay.view = "views/propertyeditors/grid2/dialogs/editconfig.html";
           $scope.editConfigCollectionOverlay.config = configValues;
           $scope.editConfigCollectionOverlay.title = title;
           $scope.editConfigCollectionOverlay.show = true;

           $scope.editConfigCollectionOverlay.submit = function(model) {

              callback(model.config)

              $scope.editConfigCollectionOverlay.show = false;
              $scope.editConfigCollectionOverlay = null;
           };

           $scope.editConfigCollectionOverlay.close = function(oldModel) {
              $scope.editConfigCollectionOverlay.show = false;
              $scope.editConfigCollectionOverlay = null;
           };

        };

        $scope.editConfig = function() {
           editConfigCollection($scope.model.value.config, "Settings", function(data) {
              $scope.model.value.config = data;
           });
        };

        $scope.editStyles = function() {
           editConfigCollection($scope.model.value.styles, "Styling", function(data){
               $scope.model.value.styles = data;
           });
        };

        /* init grid data */
        if (!$scope.model.value || $scope.model.value === "" || !$scope.model.value.templates) {
            $scope.model.value = emptyModel;
        } else {

            if (!$scope.model.value.columns) {
                $scope.model.value.columns = emptyModel.columns;
            }


            if (!$scope.model.value.config) {
                $scope.model.value.config = [];
            }

            if (!$scope.model.value.styles) {
                $scope.model.value.styles = [];
            }
        }

        /****************
            Clean up
        *****************/
        var unsubscribe = $scope.$on("formSubmitting", function (ev, args) {
            var ts = $scope.model.value.templates;
            var ls = $scope.model.value.layouts;

            _.each(ts, function(t){
                _.each(t.sections, function(section, index){
                   if(section.grid === 0){
                    t.sections.splice(index, 1);
                   }
               });
            });

            _.each(ls, function(l){
                _.each(l.areas, function(area, index){
                   if(area.grid === 0){
                    l.areas.splice(index, 1);
                   }
               });
            });
        });

        //when the scope is destroyed we need to unsubscribe
        $scope.$on('$destroy', function () {
            unsubscribe();
        });

    });
