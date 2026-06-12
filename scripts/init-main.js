db = db.getSiblingDB('urlshortener');
db.url_mappings.createIndex({ shortCode: 1 }, { unique: true });
db.url_mappings.createIndex({ longUrl: 1 }, { unique: true });
db.counters.insertOne({ _id: "url_id", seq: 0 });
