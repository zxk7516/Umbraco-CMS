/**
 * @ngdoc service
 * @name umbraco.services.dialogService
 *
 * @requires $rootScope 
 * @requires $compile
 * @requires $http
 * @requires $log
 * @requires $q
 * @requires $templateCache
 *  
 * @description
 * Application-wide service for handling modals, overlays and dialogs
 * By default it injects the passed template url into a div to body of the document
 * And renders it, but does also support rendering items in an iframe, incase
 * serverside processing is needed, or its a non-angular page
 *
 * ##usage
 * To use, simply inject the dialogService into any controller that needs it, and make
 * sure the umbraco.services module is accesible - which it should be by default.
 *
 * <pre>
 *    var dialog = dialogService.open({template: 'path/to/page.html', show: true, callback: done});
 *    functon done(data){
 *      //The dialog has been submitted 
 *      //data contains whatever the dialog has selected / attached
 *    }     
 * </pre> 
 */

angular.module('umbraco.services')
.factory('dialogService', function ($rootScope, $compile, $http, $timeout, $q, $templateCache, appState, eventsService) {

    var dialogs = [];

    /** Internal method that removes all dialogs */
    function closeAllDialogs(args) {
        for (var i = 0; i < dialogs.length; i++) {
            var dialog = dialogs[i];
            
            if(dialog.closeCallback){
                dialog.closeCallback(data);
            }
        }

        dialogs.length = 0;
    }


    /** Internal method that closes the dialog properly and cleans up resources */
    function closeDialog(dialog, data) {

        if(data && dialog.closeCallback){
            dialog.closeCallback(data);
        }

        removeDialog(dialog);
    }

    function submitDialog(dialog, data) {
        if(data && dialog.callback){
            dialog.callback(data);
        }

        removeDialog(dialog);
    }

    function removeDialog(dialog){
        var index = _.indexOf(dialogs, dialog);
        dialogs.splice(index, 1);  
    }

    /** Internal method that handles opening all dialogs */
    function openDialog(options) {
        var defaults = {
            animation: "slide-in-right",
            modalClass: "umb-modal shadow-depth-5",
            width: "100%",
            inline: false,
            iframe: false,
            template: "views/common/notfound.html",
            callback: undefined,
            closeCallback: undefined,
            element: undefined,          
            // It will set this value as a property on the dialog controller's scope as dialogData,
            // used to pass in custom data to the dialog controller's $scope. Though this is near identical to 
            // the dialogOptions property that is also set the the dialog controller's $scope object. 
            // So there's basically 2 ways of doing the same thing which we're now stuck with and in fact
            // dialogData has another specially attached property called .selection which gets used.
            dialogData: undefined
        };

        var dialog = angular.extend(defaults, options);
        dialog.cssClass = [];
        dialog.cssClass.push(dialog.modalClass);
        dialog.cssClass.push(dialog.animation);

        //push the modal into the global modal collection
        //we halt the .push because a link click will trigger a closeAll right away
        
        dialogs.push(dialog); 
        
        //Return the modal object outside of the promise!
        return dialog;        
    }

    /** Handles the closeDialogs event */
    eventsService.on("app.closeDialogs", function (evt, args) {
        closeAllDialogs(args);
    });


    return {
        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#open
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Opens a modal rendering a given template url.
         *
         * @param {Object} options rendering options
         * @param {DomElement} options.container the DOM element to inject the modal into, by default set to body
         * @param {Function} options.callback function called when the modal is submitted
         * @param {String} options.template the url of the template
         * @param {String} options.animation animation csss class, by default set to "fade"
         * @param {String} options.modalClass modal css class, by default "umb-modal"
         * @param {Bool} options.show show the modal instantly
         * @param {Bool} options.iframe load template in an iframe, only needed for serverside templates
         * @param {Int} options.width set a width on the modal, only needed for iframes
         * @param {Bool} options.inline strips the modal from any animation and wrappers, used when you want to inject a dialog into an existing container
         * @returns {Object} modal object
         */
        open: function (options) {
            return openDialog(options);
        },

        current: dialogs,

        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#close
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Closes a specific dialog
         * @param {Object} dialog the dialog object to close
         * @param {Object} args if specified this object will be sent to any "close" callbacks registered on the dialogs.
         */
        close: function (dialog, args) {
            if (dialog) {
                closeDialog(dialog, args);
            }
        },

        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#submit
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Submits and closes a specific dialog
         * @param {Object} dialog the dialog object to submit and close
         * @param {Object} args if specified this object will be sent to any "submit" callback registered on the dialogs.
         */
        submit: function (dialog, args) {
           if (dialog) {
               submitDialog(dialog, args);
           }
        },

        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#closeAll
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Closes all dialogs
         * @param {Object} args if specified this object will be sent to any callbacks registered on the dialogs.
         */
        closeAll: function (args) {
            closeAllDialogs(args);
        },

        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#mediaPicker
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Opens a media picker in a modal, the callback returns an array of selected media items
         * @param {Object} options mediapicker dialog options object
         * @param {Boolean} options.onlyImages Only display files that have an image file-extension
         * @param {Function} options.callback callback function
         * @returns {Object} modal object
         */
        mediaPicker: function (options) {
            options.template = 'views/common/dialogs/mediaPicker.html';
            return openDialog(options);
        },


        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#contentPicker
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Opens a content picker tree in a modal, the callback returns an array of selected documents
         * @param {Object} options content picker dialog options object
         * @param {Boolean} options.multipicker should the picker return one or multiple items
         * @param {Function} options.callback callback function
         * @returns {Object} modal object
         */
        contentPicker: function (options) {
            options.template = 'views/common/dialogs/contentPicker.html';
            return openDialog(options);
        },

        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#linkPicker
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Opens a link picker tree in a modal, the callback returns a single link
         * @param {Object} options content picker dialog options object
         * @param {Function} options.callback callback function
         * @returns {Object} modal object
         */
        linkPicker: function (options) {
            options.template = 'views/common/dialogs/linkPicker.html';
            return openDialog(options);
        },

        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#macroPicker
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Opens a mcaro picker in a modal, the callback returns a object representing the macro and it's parameters
         * @param {Object} options macropicker dialog options object
         * @param {Function} options.callback callback function
         * @returns {Object} modal object
         */
        macroPicker: function (options) {
            options.template = 'views/common/dialogs/insertmacro.html';
            options.modalClass = "span7 umb-modal";
            return openDialog(options);
        },

        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#memberPicker
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Opens a member picker in a modal, the callback returns a object representing the selected member
         * @param {Object} options member picker dialog options object
         * @param {Boolean} options.multiPicker should the tree pick one or multiple members before returning
         * @param {Function} options.callback callback function
         * @returns {Object} modal object
         */
        memberPicker: function (options) {
            options.template = 'views/common/dialogs/memberPicker.html';
            return openDialog(options);
        },

        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#memberGroupPicker
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Opens a member group picker in a modal, the callback returns a object representing the selected member
         * @param {Object} options member group picker dialog options object
         * @param {Boolean} options.multiPicker should the tree pick one or multiple members before returning
         * @param {Function} options.callback callback function
         * @returns {Object} modal object
         */
        memberGroupPicker: function (options) {
            options.template = 'views/common/dialogs/memberGroupPicker.html';
            return openDialog(options);
        },

        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#iconPicker
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Opens a icon picker in a modal, the callback returns a object representing the selected icon
         * @param {Object} options iconpicker dialog options object
         * @param {Function} options.callback callback function
         * @returns {Object} modal object
         */
        iconPicker: function (options) {
            options.template = 'views/common/dialogs/iconPicker.html';
            return openDialog(options);
        },

        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#treePicker
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Opens a tree picker in a modal, the callback returns a object representing the selected tree item
         * @param {Object} options iconpicker dialog options object
         * @param {String} options.section tree section to display
         * @param {String} options.treeAlias specific tree to display
         * @param {Boolean} options.multiPicker should the tree pick one or multiple items before returning
         * @param {Function} options.callback callback function
         * @returns {Object} modal object
         */
        treePicker: function (options) {
            options.template = 'views/common/dialogs/treePicker.html';
            return openDialog(options);
        },

        /**
         * @ngdoc method
         * @name umbraco.services.dialogService#propertyDialog
         * @methodOf umbraco.services.dialogService
         *
         * @description
         * Opens a dialog with a chosen property editor in, a value can be passed to the modal, and this value is returned in the callback
         * @param {Object} options mediapicker dialog options object
         * @param {Function} options.callback callback function
         * @param {String} editor editor to use to edit a given value and return on callback
         * @param {Object} value value sent to the property editor
         * @returns {Object} modal object
         */
        propertyDialog: function (options) {
            options.template = 'views/common/dialogs/property.html';
            return openDialog(options);
        },

        /**
        * @ngdoc method
        * @name umbraco.services.dialogService#ysodDialog
        * @methodOf umbraco.services.dialogService
        * @description
        * Opens a dialog to an embed dialog 
        */
        embedDialog: function (options) {
            options.template = 'views/common/dialogs/rteembed.html';
             return openDialog(options);
        },
        /**
        * @ngdoc method
        * @name umbraco.services.dialogService#ysodDialog
        * @methodOf umbraco.services.dialogService
        *
        * @description
        * Opens a dialog to show a custom YSOD
        */
        ysodDialog: function (ysodError) {

            var newScope = $rootScope.$new();
            newScope.error = ysodError;

            return openDialog({
                modalClass: "umb-modal wide",
                scope: newScope,
                //callback: options.callback,
                template: 'views/common/dialogs/ysod.html'
            });
        }
    };
});