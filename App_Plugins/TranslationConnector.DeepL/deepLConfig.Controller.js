(function () {

    'use strict';

    function configController($scope) 
    {
        var pvm = this;


        $scope.$watch('vm.settings', function (newValue, oldValue) {
            if (!angular.equals(newValue, {}))
            {
                if (newValue.split === undefined) {
                    newValue.split = true;
                }
                if (newValue.asHtml === undefined) {
                    newValue.asHtml = true;
                }
                if (newValue.useFree === undefined) {
                    newValue.useFree = true;
                }
            }
        }, true);
    }

    angular.module('umbraco')
        .controller('translate.deepLProviderController', configController);

})();