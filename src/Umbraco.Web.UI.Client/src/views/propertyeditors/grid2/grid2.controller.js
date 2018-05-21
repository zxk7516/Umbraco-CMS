/*
    Events:
    grid.initializing
    grid.initialized
    grid.rowAdded
*/

angular.module("umbraco")
    .controller("Umbraco.PropertyEditors.Grid2Controller",
    function (
        $scope,
        $http,
        assetsService,
        localizationService,
        $rootScope,
        dialogService,
        gridResource,
        mediaResource,
        imageHelper,
        $timeout,
        umbRequestHelper,
        angularHelper,
        umbDataFormatter,
        eventsService
    ) {

        // Grid status variables
        var placeHolder = "";
        var currentForm = angularHelper.getCurrentForm($scope);

        $scope.currentRow = null;
        $scope.currentCell = null;
        $scope.currentToolsControl = null;
        $scope.currentControl = null;
        $scope.openRTEToolbarId = null;
        $scope.hasSettings = false;
        $scope.showRowConfigurations = true;
        $scope.sortMode = false;
        $scope.reorderKey = "general_reorder";

        // *********************************************
        // Sortable options
        // *********************************************

        var draggedRteSettings;

        $scope.sortableOptionsRow = {
            distance: 10,
            cursor: "move",
            placeholder: "ui-sortable-placeholder",
            handle: ".umb-row-title-bar",
            helper: "clone",
            forcePlaceholderSize: true,
            tolerance: "pointer",
            zIndex: 999999999999999999,
            scrollSensitivity: 100,
            cursorAt: {
                top: 40,
                left: 60
            },

            sort: function (event, ui) {
                /* prevent vertical scroll out of the screen */
                var max = $(".umb-grid").width() - 150;
                if (parseInt(ui.helper.css("left")) > max) {
                    ui.helper.css({ "left": max + "px" });
                }
                if (parseInt(ui.helper.css("left")) < 20) {
                    ui.helper.css({ "left": 20 });
                }
            },

            start: function (e, ui) {

                // Fade out row when sorting
                ui.item.context.style.display = "block";
                ui.item.context.style.opacity = "0.5";

                draggedRteSettings = {};
                ui.item.find(".mceNoEditor").each(function () {
                    // remove all RTEs in the dragged row and save their settings
                    var id = $(this).attr("id");
                    draggedRteSettings[id] = _.findWhere(tinyMCE.editors, { id: id }).settings;
                    // tinyMCE.execCommand("mceRemoveEditor", false, id);
                });
            },

            stop: function (e, ui) {

                // Fade in row when sorting stops
                ui.item.context.style.opacity = "1";

                // reset all RTEs affected by the dragging
                ui.item.parents(".umb-column").find(".mceNoEditor").each(function () {
                    var id = $(this).attr("id");
                    draggedRteSettings[id] = draggedRteSettings[id] || _.findWhere(tinyMCE.editors, { id: id }).settings;
                    tinyMCE.execCommand("mceRemoveEditor", false, id);
                    tinyMCE.init(draggedRteSettings[id]);
                });
                currentForm.$setDirty();
            }
        };

        var notIncludedRte = [];
        var cancelMove = false;
        var startingCell;

        $scope.sortableOptionsCell = {
            distance: 10,
            cursor: "move",
            placeholder: "ui-sortable-placeholder",
            handle: ".umb-control-handle",
            helper: "clone",
            connectWith: ".umb-cell-inner",
            forcePlaceholderSize: true,
            tolerance: "pointer",
            zIndex: 999999999999999999,
            scrollSensitivity: 100,
            cursorAt: {
                top: 45,
                left: 90
            },

            sort: function (event, ui) {

                /* prevent vertical scroll out of the screen */
                var position = parseInt(ui.item.parent().offset().left) + parseInt(ui.helper.css("left")) - parseInt($(".umb-grid").offset().left);
                var max = $(".umb-grid").width() - 220;
                if (position > max) {
                    ui.helper.css({ "left": max - parseInt(ui.item.parent().offset().left) + parseInt($(".umb-grid").offset().left) + "px" });
                }
                if (position < 0) {
                    ui.helper.css({ "left": 0 - parseInt(ui.item.parent().offset().left) + parseInt($(".umb-grid").offset().left) + "px" });
                }
            },

            over: function (event, ui) {
                var cell = $(event.target).scope().cell;
                var allowedEditors = cell.allowed;

                if (($.inArray(ui.item.scope().control.editor.alias, allowedEditors) < 0 && allowedEditors) ||
                    (startingCell != cell && cell.maxItems != '' && cell.maxItems > 0 && cell.maxItems < cell.items.length + 1)) {

                    $scope.$apply(function () {
                        $(event.target).scope().cell.dropNotAllowed = true;
                    });

                    ui.placeholder.hide();
                    cancelMove = true;
                }
                else {
                    if ($(event.target).scope().cell.items.length == 0){

                        $scope.$apply(function () {
                            $(event.target).scope().cell.dropOnEmpty = true;
                        });
                        ui.placeholder.hide();
                    } else {
                        ui.placeholder.show();
                    }
                    cancelMove = false;
                }
            },

            out: function(event, ui) {
                $scope.$apply(function () {
                    $(event.target).scope().cell.dropNotAllowed = false;
                    $(event.target).scope().cell.dropOnEmpty = false;
                });
            },

            update: function (event, ui) {
                /* add all RTEs which are affected by the dragging */
                if (!ui.sender) {
                    if (cancelMove) {
                        ui.item.sortable.cancel();
                    }
                    ui.item.parents(".umb-cell.content").find(".mceNoEditor").each(function () {
                        if ($.inArray($(this).attr("id"), notIncludedRte) < 0) {
                            notIncludedRte.splice(0, 0, $(this).attr("id"));
                        }
                    });
                }
                else {
                    $(event.target).find(".mceNoEditor").each(function () {
                        if ($.inArray($(this).attr("id"), notIncludedRte) < 0) {
                            notIncludedRte.splice(0, 0, $(this).attr("id"));
                        }
                    });
                }
                currentForm.$setDirty();
            },

            start: function (e, ui) {

                //Get the starting cell for reference
                var cell = $(e.target).scope().cell;
                startingCell = cell;

                // fade out control when sorting
                ui.item.context.style.display = "block";
                ui.item.context.style.opacity = "0.5";

                // reset dragged RTE settings in case a RTE isn't dragged
                draggedRteSettings = undefined;
                ui.item.context.style.display = "block";
                ui.item.find(".mceNoEditor").each(function () {
                    notIncludedRte = [];
                    var editors = _.findWhere(tinyMCE.editors, { id: $(this).attr("id") });

                    // save the dragged RTE settings
                    if(editors) {
                        draggedRteSettings = editors.settings;

                        // remove the dragged RTE
                        tinyMCE.execCommand("mceRemoveEditor", false, $(this).attr("id"));

                    }

                });
            },

            stop: function (e, ui) {

                // Fade in control when sorting stops
                ui.item.context.style.opacity = "1";

                ui.item.parents(".umb-cell-content").find(".mceNoEditor").each(function () {
                    if ($.inArray($(this).attr("id"), notIncludedRte) < 0) {
                        // add all dragged's neighbouring RTEs in the new cell
                        notIncludedRte.splice(0, 0, $(this).attr("id"));
                    }
                });
                $timeout(function () {
                    // reconstruct the dragged RTE (could be undefined when dragging something else than RTE)
                    if (draggedRteSettings !== undefined) {
                        tinyMCE.init(draggedRteSettings);
                    }

                    _.forEach(notIncludedRte, function (id) {
                        // reset all the other RTEs
                        if (draggedRteSettings === undefined || id !== draggedRteSettings.id) {
                            var rteSettings = _.findWhere(tinyMCE.editors, { id: id }).settings;
                            tinyMCE.execCommand("mceRemoveEditor", false, id);
                            tinyMCE.init(rteSettings);
                        }
                    });
                }, 500, false);

                $scope.$apply(function () {

                    var cell = $(e.target).scope().cell;
                    cell.hasActiveChild = hasActiveChild(cell, cell.items);
                    cell.active = false;
                });
            }

        };

        $scope.toggleSortMode = function() {
            $scope.sortMode = !$scope.sortMode;
            if($scope.sortMode) {
                $scope.reorderKey = "general_reorderDone";
            } else {
                $scope.reorderKey = "general_reorder";
            }
        };

        $scope.showReorderButton = function() {
            if($scope.model.value && $scope.model.value.sections) {
                for(var i = 0; $scope.model.value.sections.length > i; i++) {
                    var section = $scope.model.value.sections[i];
                    if(section.rows && section.rows.length > 0) {
                        return true;
                    }
                }
            }
        };

        // *********************************************
        // Add items overlay menu
        // *********************************************
        $scope.openEditorOverlay = function (event, cell, index, key) {
          $scope.editorOverlay = {
              view: "itempicker",
              filter: cell.$allowedEditors.length > 15,
              title: localizationService.localize("grid_insertControl"),
              availableItems: cell.$allowedEditors,
              event: event,
              show: true,
              submit: function(model) {
                  $scope.addControl(model.selectedItem, cell, index);
                  $scope.editorOverlay.show = false;
                  $scope.editorOverlay = null;
              }
          };
       };
        

        // *********************************************
        // Row management function
        // *********************************************

        $scope.clickRow = function(index, rows) {
            rows[index].active = true;
        };

        $scope.clickOutsideRow = function(index, rows) {
            rows[index].active = false;
        };

        function getAllowedRowLayouts(section) {

            var rowLayouts = $scope.model.config.items.layouts;

            // fixme - remove when refactored datatype
            _.forEach(rowLayouts, function(rowLayout) {
                if (!rowLayout.cells) {
                    rowLayout.cells = rowLayout.areas;
                    delete rowLayout.areas;
                }
            });

            return rowLayouts;

            //fixme - this is probably not necessary any more

            //This will occur if it is a new section which has been
            // created from a 'template'
            if (section.allowed && section.allowed.length > 0) {
                return _.filter(rowLayouts, function (layout) {
                    return _.indexOf(section.allowed, layout.name) >= 0;
                });
            }
            else {


                return rowLayouts;
            }
        }

        $scope.addRow = function (section, rowLayout) {

            //copy the selected layout into the rows collection
            var row = angular.copy(rowLayout);

            //fixme - we've got discrepancies between alias/name on row layouts and rows.
            row.alias = row.name;

            // Init row value
            row = initRow(row);

            // Push the new row
            if (row) {
                section.rows.push(row);
            }

            currentForm.$setDirty();

            $scope.showRowConfigurations = false;

            eventsService.emit("grid.rowAdded", { el: $scope.$el, scope: $scope, row: row });

        };

        $scope.removeRow = function (section, $index) {
            if (section.rows.length > 0) {
                section.rows.splice($index, 1);
                $scope.currentRow = null;
                $scope.openRTEToolbarId = null;
                currentForm.$setDirty();
            }

            if(section.rows.length === 0) {
               $scope.showRowConfigurations = true;
            }
        };

        var shouldApply = function(item, itemType, gridItem) {
            if (item.applyTo === undefined || item.applyTo === null || item.applyTo === "") {
                return true;
            }

            if (typeof (item.applyTo) === "string") {
                return item.applyTo === itemType;
            }

            if (itemType === "row") {
                if (item.applyTo.row === undefined) {
                    return false;
                }
                if (item.applyTo.row === null || item.applyTo.row === "") {
                    return true;
                }
                var rows = item.applyTo.row.split(',');
                return _.indexOf(rows, gridItem.name) !== -1;
            } else if (itemType === "cell") {
                if (item.applyTo.cell === undefined) {
                    return false;
                }
                if (item.applyTo.cell === null || item.applyTo.cell === "") {
                    return true;
                }
                var cells = item.applyTo.cell.split(',');
                var cellSize = gridItem.grid.toString();
                return _.indexOf(cells, cellSize) !== -1;
            }
        }

        $scope.editGridItemSettings = function (gridItem, itemType) {

            placeHolder = "{0}";

            var styles, config;
            if (itemType === 'control') {
                styles = null;
                config = angular.copy(gridItem.editor.config.settings);
            } else {
                styles = _.filter(angular.copy($scope.model.config.items.styles), function (item) { return shouldApply(item, itemType, gridItem); });
                config = _.filter(angular.copy($scope.model.config.items.config), function (item) { return shouldApply(item, itemType, gridItem); });
            }

            if(angular.isObject(gridItem.config)){
                _.each(config, function(cfg){
                    var val = gridItem.config[cfg.key];
                    if(val){
                        cfg.value = stripModifier(val, cfg.modifier);
                    }
                });
            }

            if(angular.isObject(gridItem.styles)){
                _.each(styles, function(style){
                    var val = gridItem.styles[style.key];
                    if(val){
                        style.value = stripModifier(val, style.modifier);
                    }
                });
            }

            $scope.gridItemSettingsDialog = {};
            $scope.gridItemSettingsDialog.view = "views/propertyeditors/grid/dialogs/config.html";
            $scope.gridItemSettingsDialog.title = "Settings";
            $scope.gridItemSettingsDialog.styles = styles;
            $scope.gridItemSettingsDialog.config = config;

            $scope.gridItemSettingsDialog.show = true;

            $scope.gridItemSettingsDialog.submit = function(model) {

                var styleObject = {};
                var configObject = {};

                _.each(model.styles, function(style){
                    if(style.value){
                        styleObject[style.key] = addModifier(style.value, style.modifier);
                    }
                });
                _.each(model.config, function (cfg) {
                    if (cfg.value) {
                        configObject[cfg.key] = addModifier(cfg.value, cfg.modifier);
                    }
                });

                gridItem.styles = styleObject;
                gridItem.config = configObject;
                gridItem.hasConfig = gridItemHasConfig(styleObject, configObject);

                currentForm.$setDirty();

                $scope.gridItemSettingsDialog.show = false;
                $scope.gridItemSettingsDialog = null;
            };

            $scope.gridItemSettingsDialog.close = function(oldModel) {
                $scope.gridItemSettingsDialog.show = false;
                $scope.gridItemSettingsDialog = null;
            };

        };

        function stripModifier(val, modifier) {
            if (!val || !modifier || modifier.indexOf(placeHolder) < 0) {
                return val;
            } else {
                var paddArray = modifier.split(placeHolder);
                if(paddArray.length == 1){
                    if (modifier.indexOf(placeHolder) === 0) {
                        return val.slice(0, -paddArray[0].length);
                    } else {
                        return val.slice(paddArray[0].length, 0);
                    }
                } else {
                    if (paddArray[1].length === 0) {
                        return val.slice(paddArray[0].length);
                    }
                    return val.slice(paddArray[0].length, -paddArray[1].length);
                }
            }
        }

        var addModifier = function(val, modifier){
            if (!modifier || modifier.indexOf(placeHolder) < 0) {
                return val;
            } else {
                return modifier.replace(placeHolder, val);
            }
        };

        function gridItemHasConfig(styles, config) {

            if(_.isEmpty(styles) && _.isEmpty(config)) {
                return false;
            } else {
                return true;
            }

        }

        // *********************************************
        // cell management functions
        // *********************************************

        $scope.clickCell = function(index, cells, row) {
            cells[index].active = true;
            row.hasActiveChild = true;
        };

        $scope.clickOutsideCell = function(index, cells, row) {
            cells[index].active = false;
            row.hasActiveChild = hasActiveChild(row, cells);
        };

        $scope.cellPreview = function (cell) {
            if (cell && cell.$allowedEditors) {
                var editor = cell.$allowedEditors[0];
                return editor.icon;
            } else {
                return "icon-layout";
            }
        };


        // *********************************************
        // Control management functions
        // *********************************************
        $scope.clickControl = function (index, items, cell) {
            items[index].active = true;
            cell.hasActiveChild = true;
        };

        $scope.clickOutsideControl = function (index, items, cell) {
            items[index].active = false;
            cell.hasActiveChild = hasActiveChild(cell, items);
        };

        function hasActiveChild(item, children) {

            var activeChild = false;

            for(var i = 0; children.length > i; i++) {
                var child = children[i];

                if(child.active) {
                    activeChild = true;
                }
            }

            if(activeChild) {
                return true;
            }

        }


        var guid = (function () {
            function s4() {
                return Math.floor((1 + Math.random()) * 0x10000)
                           .toString(16)
                           .substring(1);
            }
            return function () {
                return s4() + s4() + "-" + s4() + "-" + s4() + "-" +
                       s4() + "-" + s4() + s4() + s4();
            };
        })();
        
        $scope.addControl = function (editor, cell, index, initialize) {

            initialize = (initialize !== false);

            var newItem = {
                type: editor.udi,
                values: {},
                //editor: editor,
                $initializing: initialize
            };

            if (index === undefined) {
                index = cell.items.length;
            }

            newItem.active = true;

            //populate control
            initItem(newItem, index + 1);

            cell.items.push(newItem);

            eventsService.emit("grid.itemAdded", { el: $scope.$el, scope: $scope, cell: cell, item: newItem });

        };

        $scope.addTinyMce = function (cell) {
            var rte = getEditor("rte");
            $scope.addControl(rte, cell);
        };

        function getEditor(alias) {
            return _.find($scope.availableEditors, function (editor) { return editor.alias === alias; });
        }

        function getEditorByUdi(udi) {
            return _.find($scope.availableEditors, function (editor) { return editor.udi === udi; });
        }

        $scope.removeControl = function (cell, $index) {
            $scope.currentControl = null;
            cell.items.splice($index, 1);
        };

        $scope.percentage = function (spans) {
            return ((spans / $scope.model.config.items.columns) * 100).toFixed(8);
        };


        $scope.clearPrompt = function (scopedObject, e) {
            scopedObject.deletePrompt = false;
            e.preventDefault();
            e.stopPropagation();
        };

        $scope.togglePrompt = function (scopedObject) {
            scopedObject.deletePrompt = !scopedObject.deletePrompt;
        };

        $scope.hidePrompt = function (scopedObject) {
            scopedObject.deletePrompt = false;
        };

        $scope.toggleAddRow = function() {
          $scope.showRowConfigurations = !$scope.showRowConfigurations;
        };


        // *********************************************
        // Initialization - this runs ONE time
        // these methods are called from ng-init on the template
        // so we can controll their first load data
        //
        // intialization sets non-saved data like percentage sizing, allowed editors and
        // other data that should all be pre-fixed with $ to strip it out on save
        // *********************************************

        // *********************************************
        // Init template + sections
        // *********************************************
        function initContent () {
            //fixme - config will be different we need to wait for that 
            ////settings indicator shortcut
            //if (($scope.model.config.items.config && $scope.model.config.items.config.length > 0) || ($scope.model.config.items.styles && $scope.model.config.items.styles.length > 0)) {
            //    $scope.hasSettings = true;
            //}

            //ensure the grid has a column value set, if nothing is found, set it to 12
            if (!$scope.model.config.items.columns){
                $scope.model.config.items.columns = 12;
            } else if (angular.isString($scope.model.config.items.columns)) {
                $scope.model.config.items.columns = parseInt($scope.model.config.items.columns);
            }

            //fixme - we are currently still using "layouts" but we will eventually get rid of them. So we need to wrap
            //our current value with the layout JS since we only persist with rows.
            var section = {
                grid: $scope.model.config.items.columns,
                rows: []
            };
            var sectionWrapper = {
                name: "1 column layout",
                sections: [
                    section
                ]
            };

            angular.extend(sectionWrapper.sections[0], $scope.model.value);
            $scope.model.value = sectionWrapper;
            
            initSection(section);
            
        };

        function initSection(section) {
            section.$percentage = $scope.percentage(section.grid);

            section.allowedRowLayouts = getAllowedRowLayouts(section);

            if (!section.rows || section.rows.length === 0) {
                section.rows = [];
                if (section.allowedRowLayouts.length === 1){
                    $scope.addRow(section, section.allowedRowLayouts[0]);
                }
            } else {
                _.forEach(section.rows, function (row, index) {
                    if (!row.$initialized) {
                        var initd = initRow(row);

                        //if init fails, remove
                        if (!initd) {
                            section.rows.splice(index, 1);
                        } else {
                            section.rows[index] = initd;
                        }
                    }
                });

                // if there is more than one row added - hide row add tools
                $scope.showRowConfigurations = false;
            }
        };


        // *********************************************
        // Init layout / row
        // *********************************************
        function initRow (row) {

            //merge the layout data with the original config data
            //if there are no config info on this, splice it out
            var original = _.find($scope.model.config.items.layouts, function (o) { return o.name === row.alias; });

            if (!original) {
                return null;
            } else {

                //make a copy to not touch the original config
                original = angular.copy(original);
                original.styles = row.styles;
                original.config = row.config;
                original.hasConfig = gridItemHasConfig(row.styles, row.config);


                //sync cell configuration
                _.each(original.cells, function (cell, cellIndex) {


                    if (cell.grid > 0) {
                        var currentCell = row.cells[cellIndex];

                        if (currentCell) {
                            cell.config = currentCell.config;
                            cell.styles = currentCell.styles;
                            cell.hasConfig = gridItemHasConfig(currentCell.styles, currentCell.config);
                        }

                        //set editor permissions
                        if (!cell.allowed || cell.allowAll === true) {
                            cell.$allowedEditors = $scope.availableEditors;
                            cell.$allowsRTE = true;
                        } else {
                            cell.$allowedEditors = _.filter($scope.availableEditors, function (editor) {
                                return _.indexOf(cell.allowed, editor.udi) >= 0;
                            });

                            if (_.indexOf(cell.allowed, "rte") >= 0) {
                                cell.$allowsRTE = true;
                            }
                        }

                        //copy over existing items into the new cells
                        if (row.cells.length > cellIndex && row.cells[cellIndex].items) {
                            cell.items = currentCell.items;

                            _.forEach(cell.items, function (item, itemIndex) {
                                initItem(item, itemIndex);
                            });

                        } else {
                            //if empty
                            cell.items = [];

                            //if only one allowed editor
                            if (cell.$allowedEditors.length === 1){
                                $scope.addControl(cell.$allowedEditors[0], cell, 0, false);
                            }
                        }

                        //set width
                        cell.$percentage = $scope.percentage(cell.grid);
                        cell.$uniqueId = guid();

                    } else {
                        original.cells.splice(cellIndex, 1);
                    }
                });

                //replace the old row
                original.$initialized = true;

                //set a disposable unique ID
                original.$uniqueId = guid();

                //set a no disposable unique ID (util for row styling)
                original.id = !row.id ? guid() : row.id;

                return original;
            }

        }


        // *********************************************
        // Init control
        // *********************************************
        
        function initItem(item, index) {
            item.$index = index;
            item.$uniqueId = guid();

            //create the properties collection which will be bound here
            item.properties = [];

            //get a scaffold for the current doc type
            gridResource.getScaffold(item.type).then(function (c) {
                if (c.tabs && c.tabs.length) {
                    item.properties = c.tabs[0].properties;
                    _.each(item.properties, function (p) {
                        p.$uniqueId = guid();
                        p.hideLabel = true;
                        p.value = item.values[p.alias];
                        
                        var editor = getEditorByUdi(item.type);
                        
                        //now we need to re-assign the view and set the boolean if it's a preview or not
                        if (editor.views && editor.views[p.alias]) {
                            p.view = editor.views[p.alias].view;
                        }
                        else {
                            //show the icon
                            p.view = "views/propertyeditors/grid2/cellplaceholder.html";
                            p.icon = editor.icon;
                            p.title = editor.name;
                        }
                    });
                }
            });

        };

        /** Called once to initialize the editor */
        function init() {
            gridResource.getGridContentTypes().then(function (response) {
                $scope.availableEditors = response;

                //Localize the grid editor names
                angular.forEach($scope.availableEditors, function (value, key) {
                    //If no translation is provided, keep using the editor name from the manifest
                    if (localizationService.dictionary.hasOwnProperty("grid_" + value.alias)) {
                        value.name = localizationService.localize("grid_" + value.alias);
                    }
                });

                $scope.contentReady = true;

                eventsService.emit("grid.initializing", { el: $scope.$el, scope: $scope });

                //Init grid
                initContent();

                eventsService.emit("grid.initialized", { el: $scope.$el, scope: $scope });

            });
        }

        init();
        
        //Clean the grid value before submitting to the server, we don't need
        // all of that grid configuration in the value to be stored!! All of that
        // needs to be merged in at runtime to ensure that the real config values are used
        // if they are ever updated.

        var unsubscribe = $scope.$on("formSubmitting", function () {
            $scope.model.value = umbDataFormatter.mapGridValueToPersistableModel($scope.model.value);
        });

        //when the scope is destroyed we need to unsubscribe
        $scope.$on("$destroy", function () {
            unsubscribe();
        });

    });
