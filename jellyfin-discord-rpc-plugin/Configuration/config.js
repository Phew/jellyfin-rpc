define(["loading","emby-button","emby-input"], function() {
    'use strict';

    const pluginId = '7f1e77a0-6e64-4b3c-9a78-2f6f3e23f2f6';

    function loadConfig(page) {
        ApiClient.getPluginConfiguration(pluginId).then(function(config) {
            page.querySelector('#detailsTemplate').value = config.DetailsTemplate || '';
            page.querySelector('#stateTemplate').value = config.StateTemplate || '';
            page.querySelector('#largeImageKey').value = config.LargeImageKey || '';
            page.querySelector('#largeImageTextTemplate').value = config.LargeImageTextTemplate || '';
            page.querySelector('#smallImageKey').value = config.SmallImageKey || '';
            page.querySelector('#smallImageTextTemplate').value = config.SmallImageTextTemplate || '';
            page.querySelector('#includeTimestamps').checked = !!config.IncludeTimestamps;
        });
    }

    function saveConfig(page) {
        return ApiClient.getPluginConfiguration(pluginId).then(function(config) {
            config.DetailsTemplate = page.querySelector('#detailsTemplate').value;
            config.StateTemplate = page.querySelector('#stateTemplate').value;
            config.LargeImageKey = page.querySelector('#largeImageKey').value;
            config.LargeImageTextTemplate = page.querySelector('#largeImageTextTemplate').value;
            config.SmallImageKey = page.querySelector('#smallImageKey').value;
            config.SmallImageTextTemplate = page.querySelector('#smallImageTextTemplate').value;
            config.IncludeTimestamps = page.querySelector('#includeTimestamps').checked;
            return ApiClient.updatePluginConfiguration(pluginId, config).then(function() {
                Dashboard.processPluginConfigurationUpdateResult();
            });
        });
    }

    return {
        onShow: function() {},
        onHide: function() {},
        onRendered: function(page) {
            loadConfig(page);
            const form = page.querySelector('.discordrpcConfigurationForm');
            form.addEventListener('submit', function(e) {
                e.preventDefault();
                saveConfig(page);
            });
        }
    };
});

