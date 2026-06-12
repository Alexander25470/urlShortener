db = db.getSiblingDB('urlshortener_analytics');
db.createCollection('clicks', {
  timeseries: {
    timeField: 'timestamp',
    metaField: 'shortCode',
    granularity: 'seconds'
  }
});
