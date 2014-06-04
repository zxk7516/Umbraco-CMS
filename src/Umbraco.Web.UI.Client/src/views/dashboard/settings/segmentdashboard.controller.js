function segmentDashboardController($scope, umbRequestHelper, $log, $http, formHelper) {

    function refreshData() {
        return umbRequestHelper.resourcePromise(
                $http.get(umbRequestHelper.getApiUrl("segmentDashboardApiBaseUrl", "GetProviders")),
                "Failed to retrieve provider data")
            .then(function(data) {
                $scope.providers = data;
            });
    }

    $scope.providers = [];
    $scope.configuringSegments = false;
    $scope.providerConfig = {
        providerName: "",
        providerTypeName: "",
    }
    $scope.segmentConfig = [];
    $scope.variantConfig = [];

    $scope.toggle = function(provider) {
        umbRequestHelper.resourcePromise(
                $http.post(
                    umbRequestHelper.getApiUrl("segmentDashboardApiBaseUrl", "PostToggleProvider",
                    [{ typeName: provider.typeName }])),
                'Failed to toggle segment provider')
            .then(function() {
                //toggle local var
                provider.enabled = !provider.enabled;
            });
    }

    $scope.showSegmentConfig = function (item) {
        
        $scope.configuringSegments = true;
        $scope.providerConfig.providerName = item.displayName;
        $scope.providerConfig.providerTypeName = item.typeName;
        $scope.segmentConfig = item.segmentConfig;
    }

    $scope.showVariantConfig = function (item) {

        $scope.configuringVariants = true;
        $scope.providerConfig.providerName = item.displayName;
        $scope.providerConfig.providerTypeName = item.typeName;
        $scope.variantConfig = item.variantConfig;
    }

    $scope.cancel = function () {

        formHelper.resetForm({ scope: $scope });

        $scope.configuringVariants = false;
        $scope.configuringSegments = false;

        $scope.providerConfig = {
            providerName: "",
            providerTypeName: "",
        }
        $scope.segmentConfig = [];
        $scope.variantConfig = [];
    }

    $scope.addItem = function() {
        $scope.segmentConfig.push({ matchExpression: "", key: "", value: "" });
    }

    $scope.removeItem = function(item) {
        $scope.segmentConfig = _.reject($scope.segmentConfig, function (i) { return i === item; });
    }

    $scope.saveSegmentConfig = function (formCtrl) {

        if (formHelper.submitForm({ scope: $scope, formCtrl: formCtrl })) {

            umbRequestHelper.resourcePromise(
                $http.post(
                    umbRequestHelper.getApiUrl("segmentDashboardApiBaseUrl", "PostSaveProviderSegmentConfig",
                    [{ typeName: $scope.providerConfig.providerTypeName }]),
                    $scope.segmentConfig),
                'Failed to save segment configuration')
            .then(function () {

                //now that its successful, save the persisted values back to the provider's values
                var provider = _.find($scope.providers, function (i) { return i.typeName === $scope.providerConfig.providerTypeName; });
                provider.segmentConfig = $scope.segmentConfig;

                refreshData().then(function() {
                    //exit editing config
                    $scope.cancel();
                });

            });
        }
    }

    $scope.saveVariantConfig = function (formCtrl) {
        
        umbRequestHelper.resourcePromise(
                $http.post(
                    umbRequestHelper.getApiUrl("segmentDashboardApiBaseUrl", "PostSaveProviderVariantConfig",
                    [{ typeName: $scope.providerConfig.providerTypeName }]),
                    $scope.variantConfig),
                'Failed to save variant configuration')
            .then(function () {

                //now that its successful, save the persisted values back to the provider's values
                var provider = _.find($scope.providers, function (i) { return i.typeName === $scope.providerConfig.providerTypeName; });
                provider.variantConfig = $scope.variantConfig;

                refreshData().then(function () {
                    //exit editing config
                    $scope.cancel();
                });
            });
    }
    
    //initial data load
    refreshData();
}
angular.module("umbraco").controller("Umbraco.Dashboard.SegmentDashboard", segmentDashboardController);