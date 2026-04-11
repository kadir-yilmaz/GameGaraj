// MongoDB Database Drop Script
// Run with: mongosh "mongodb://localhost:27017" drop-database.js

use catalogdb;

print("Dropping all collections in catalogdb...");

db.products.drop();
db.categories.drop();
db.categoryAttributes.drop();
db._seed_metadata.drop();

print("✅ All collections dropped successfully!");
print("Restart the Catalog.API to re-seed the database.");
