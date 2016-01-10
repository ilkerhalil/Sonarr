var _ = require('underscore');
var EpisodeModel = require('Series/EpisodeModel');
var PagableCollection = require('backbone.paginator');
var AsFilteredCollection = require('Mixins/AsFilteredCollection');
var AsSortedCollection = require('Mixins/AsSortedCollection');
var AsPersistedStateCollection = require('Mixins/AsPersistedStateCollection');

var Collection = PagableCollection.extend({
  url: window.Sonarr.ApiRoot + '/wanted/cutoff',
  model: EpisodeModel,
  tableName: 'wanted.cutoff',

  state: {
    pageSize: 15,
    sortKey: 'airDateUtc',
    order: 1
  },

  queryParams: {
    totalPages: null,
    totalRecords: null,
    pageSize: 'pageSize',
    sortKey: 'sortKey',
    order: 'sortDir',
    directions: {
      '-1': 'asc',
      '1': 'desc'
    }
  },

  filterModes: {
    'monitored': {
      key: 'monitored',
      value: 'true'
    },
    'unmonitored': {
      key: 'monitored',
      value: 'false'
    }
  },

  sortMappings: {
    'series': { sortKey: 'series.sortTitle' }
  },

  parseState: function(resp) {
    return { totalRecords: resp.totalRecords };
  },

  parseRecords: function(resp) {
    if (resp) {
      return resp.records;
    }

    return resp;
  }
});

Collection = AsFilteredCollection.call(Collection);
Collection = AsSortedCollection.call(Collection);

module.exports = AsPersistedStateCollection.call(Collection);